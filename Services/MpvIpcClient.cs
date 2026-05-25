using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace DenoPlayer.Services;

/// <summary>
/// mpv JSON IPC 클라이언트. Windows named pipe(`\\.\pipe\name`) 한쪽 끝.
/// - 명령: 라인 단위 JSON, "\n" 종결
/// - 응답: {"request_id":N, "error":"success", "data":...}
/// - 이벤트: {"event":"property-change"|"end-file"|...}
/// 재시도는 짧게만, 무한 재시도 금지.
/// </summary>
public sealed class MpvIpcClient : IDisposable
{
    public event Action<string, JsonElement?>? PropertyChanged;   // (name, value)
    public event Action<string?>?               EndFile;          // reason
    public event Action?                        FileLoaded;
    public event Action?                        PlaybackRestart;
    public event Action?                        Connected;
    public event Action?                        Disconnected;

    private NamedPipeClientStream? _pipe;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private CancellationTokenSource? _cts;
    private Task? _readLoop;

    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly System.Collections.Concurrent.ConcurrentDictionary<long, TaskCompletionSource<JsonElement>> _pending = new();
    private long _nextRequestId;
    private long _nextObserveId = 100;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public bool IsConnected => _pipe?.IsConnected == true;

    public async Task<bool> ConnectAsync(string pipeName, TimeSpan timeout, CancellationToken ct = default)
    {
        Dispose();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        var pipe = new NamedPipeClientStream(".", pipeName,
            PipeDirection.InOut, PipeOptions.Asynchronous);

        try
        {
            await pipe.ConnectAsync((int)timeout.TotalMilliseconds, _cts.Token).ConfigureAwait(false);
        }
        catch
        {
            pipe.Dispose();
            return false;
        }

        _pipe = pipe;
        _reader = new StreamReader(pipe, new UTF8Encoding(false));
        _writer = new StreamWriter(pipe, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };
        _readLoop = Task.Run(() => ReadLoopAsync(_cts.Token));

        Connected?.Invoke();
        return true;
    }

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && _reader is not null && _pipe?.IsConnected == true)
            {
                string? line;
                try { line = await _reader.ReadLineAsync(ct).ConfigureAwait(false); }
                catch { break; }
                if (line is null) break;
                if (line.Length == 0) continue;

                JsonDocument doc;
                try { doc = JsonDocument.Parse(line); }
                catch { continue; } // mpv가 가끔 비표준 라인을 흘릴 수 있음

                using (doc)
                {
                    var root = doc.RootElement;
                    if (root.TryGetProperty("request_id", out var reqEl) &&
                        reqEl.TryGetInt64(out var reqId) &&
                        _pending.TryRemove(reqId, out var tcs))
                    {
                        if (root.TryGetProperty("error", out var errEl) &&
                            errEl.ValueKind == JsonValueKind.String &&
                            errEl.GetString() == "success")
                        {
                            var data = root.TryGetProperty("data", out var d)
                                ? d.Clone()
                                : default;
                            tcs.TrySetResult(data);
                        }
                        else
                        {
                            var err = root.TryGetProperty("error", out var e) ? e.GetString() : "unknown";
                            tcs.TrySetException(new IOException("mpv error: " + err));
                        }
                        continue;
                    }

                    if (root.TryGetProperty("event", out var evEl) && evEl.ValueKind == JsonValueKind.String)
                    {
                        var ev = evEl.GetString();
                        switch (ev)
                        {
                            case "property-change":
                                {
                                    var name = root.TryGetProperty("name", out var n) ? n.GetString() : null;
                                    JsonElement? data = root.TryGetProperty("data", out var d) ? d.Clone() : null;
                                    if (!string.IsNullOrEmpty(name))
                                        PropertyChanged?.Invoke(name!, data);
                                    break;
                                }
                            case "end-file":
                                {
                                    var reason = root.TryGetProperty("reason", out var r) ? r.GetString() : null;
                                    EndFile?.Invoke(reason);
                                    break;
                                }
                            case "file-loaded":
                                FileLoaded?.Invoke();
                                break;
                            case "playback-restart":
                                PlaybackRestart?.Invoke();
                                break;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[mpv-ipc] read loop crashed: {ex}");
        }
        finally
        {
            Disconnected?.Invoke();
        }
    }

    // ------------ 송신 ------------

    public Task<JsonElement> CommandAsync(params object[] args) => CommandAsync((IEnumerable<object>)args);

    public async Task<JsonElement> CommandAsync(IEnumerable<object> args)
    {
        if (_writer is null || _pipe?.IsConnected != true)
            throw new InvalidOperationException("mpv IPC not connected");

        var id = Interlocked.Increment(ref _nextRequestId);
        var tcs = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = tcs;

        var payload = JsonSerializer.Serialize(new
        {
            command = args.ToArray(),
            request_id = id
        }, JsonOpts);

        await _writeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await _writer.WriteLineAsync(payload).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }

        // 응답이 끝내 안 오면 4초 후 포기 — 무한 대기 금지
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(4));
        timeout.Token.Register(() =>
        {
            if (_pending.TryRemove(id, out var t))
                t.TrySetException(new TimeoutException($"mpv command timeout: id={id}"));
        });
        return await tcs.Task.ConfigureAwait(false);
    }

    /// <summary>fire-and-forget. 빈번한 seek 등에서 응답을 기다리지 않을 때.</summary>
    public async Task SendAsync(params object[] args)
    {
        if (_writer is null || _pipe?.IsConnected != true) return;
        var payload = JsonSerializer.Serialize(new { command = args }, JsonOpts);
        await _writeLock.WaitAsync().ConfigureAwait(false);
        try { await _writer.WriteLineAsync(payload).ConfigureAwait(false); }
        catch { /* swallowed: best-effort */ }
        finally { _writeLock.Release(); }
    }

    // ------------ 편의 메서드 ------------

    public Task<JsonElement> ObserveProperty(string name)
    {
        var id = Interlocked.Increment(ref _nextObserveId);
        return CommandAsync("observe_property", id, name);
    }

    public Task LoadFile(string path)      => SendAsync("loadfile", path, "replace");
    public Task SetPause(bool pause)       => SendAsync("set_property", "pause", pause);
    public Task SetVolume(double vol)      => SendAsync("set_property", "volume", vol);
    public Task SetMute(bool mute)         => SendAsync("set_property", "mute", mute);
    public Task SetSpeed(double speed)     => SendAsync("set_property", "speed", speed);
    public Task SeekAbsolute(double sec)   => SendAsync("seek", sec, "absolute", "exact");
    public Task SeekRelative(double sec)   => SendAsync("seek", sec, "relative");
    public Task Stop()                     => SendAsync("stop");
    public Task FrameStep()                => SendAsync("frame-step");
    public Task FrameBackStep()            => SendAsync("frame-back-step");

    /// <summary>
    /// mpv screenshot-to-file. mpv가 실패하면(권한/디스크/경로 문자 등) error 응답 옴.
    /// CommandAsync로 보내 응답 확인 → 호출자가 toast 메시지 정확히 결정 가능.
    /// </summary>
    public async Task<bool> ScreenshotChecked(string path)
    {
        try { await CommandAsync("screenshot-to-file", path); return true; }
        catch { return false; }
    }
    public Task Screenshot(string path)    => SendAsync("screenshot-to-file", path);

    // ─ 자막/오디오 트랙 사이클 — 다국어 영상 / 다중 자막 영상에서 즉시 전환 ─
    public Task CycleSubtitle()            => SendAsync("cycle", "sub");
    public Task CycleSubtitleVisibility()  => SendAsync("cycle", "sub-visibility");
    public Task CycleAudio()               => SendAsync("cycle", "audio");
    public Task LoadSubtitle(string path)  => SendAsync("sub-add", path, "select");

    public void Dispose()
    {
        try { _cts?.Cancel(); } catch { }
        try { _writer?.Dispose(); } catch { }
        try { _reader?.Dispose(); } catch { }
        try { _pipe?.Dispose(); } catch { }
        _writer = null; _reader = null; _pipe = null;

        foreach (var kv in _pending)
            kv.Value.TrySetException(new ObjectDisposedException(nameof(MpvIpcClient)));
        _pending.Clear();
    }
}

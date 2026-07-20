using System.Diagnostics;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace DenoVideoPlayer.Services;

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

    private sealed class Connection
    {
        public required long Generation { get; init; }
        public required NamedPipeClientStream Pipe { get; init; }
        public required StreamReader Reader { get; init; }
        public required StreamWriter Writer { get; init; }
        public required CancellationTokenSource Cancellation { get; init; }
        public Task? ReadLoop { get; set; }
        public int DisconnectNotified;
    }

    private sealed record PendingRequest(long Generation, TaskCompletionSource<JsonElement> Completion);

    private Connection? _connection;
    private long _nextConnectionGeneration;
    private int _disposed;

    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly SemaphoreSlim _connectGate = new(1, 1);
    private readonly ConcurrentDictionary<long, PendingRequest> _pending = new();
    private long _nextRequestId;
    private long _nextObserveId = 100;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public bool IsConnected => Volatile.Read(ref _connection)?.Pipe.IsConnected == true;

    public async Task<bool> ConnectAsync(string pipeName, TimeSpan timeout, CancellationToken ct = default)
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(MpvIpcClient));

        await _connectGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await DisconnectCurrentAsync(suppressEvent: true).ConfigureAwait(false);

            var cancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var pipe = new NamedPipeClientStream(".", pipeName,
                PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            try
            {
                await pipe.ConnectAsync((int)timeout.TotalMilliseconds, cancellation.Token)
                    .ConfigureAwait(false);
            }
            catch
            {
                cancellation.Dispose();
                pipe.Dispose();
                return false;
            }

            var connection = new Connection
            {
                Generation = Interlocked.Increment(ref _nextConnectionGeneration),
                Pipe = pipe,
                Reader = new StreamReader(pipe, new UTF8Encoding(false)),
                Writer = new StreamWriter(pipe, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" },
                Cancellation = cancellation,
            };
            Volatile.Write(ref _connection, connection);
            connection.ReadLoop = Task.Run(() => ReadLoopAsync(connection));

            InvokeSafely(Connected);
            return true;
        }
        finally
        {
            _connectGate.Release();
        }
    }

    private async Task ReadLoopAsync(Connection connection)
    {
        var ct = connection.Cancellation.Token;
        try
        {
            while (!ct.IsCancellationRequested && connection.Pipe.IsConnected)
            {
                string? line;
                try { line = await connection.Reader.ReadLineAsync(ct).ConfigureAwait(false); }
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
                        _pending.TryRemove(reqId, out var pending) &&
                        pending.Generation == connection.Generation)
                    {
                        if (root.TryGetProperty("error", out var errEl) &&
                            errEl.ValueKind == JsonValueKind.String &&
                            errEl.GetString() == "success")
                        {
                            var data = root.TryGetProperty("data", out var d)
                                ? d.Clone()
                                : default;
                            pending.Completion.TrySetResult(data);
                        }
                        else
                        {
                            var err = root.TryGetProperty("error", out var e) ? e.GetString() : "unknown";
                            pending.Completion.TrySetException(new IOException("mpv error: " + err));
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
                                        InvokeSafely(PropertyChanged, name!, data);
                                    break;
                                }
                            case "end-file":
                                {
                                    var reason = root.TryGetProperty("reason", out var r) ? r.GetString() : null;
                                    InvokeSafely(EndFile, reason);
                                    break;
                                }
                            case "file-loaded":
                                InvokeSafely(FileLoaded);
                                break;
                            case "playback-restart":
                                InvokeSafely(PlaybackRestart);
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
            if (ReferenceEquals(Interlocked.CompareExchange(ref _connection, null, connection), connection))
            {
                FailPending(connection.Generation, new IOException("mpv IPC disconnected"));
                NotifyDisconnected(connection);
            }
        }
    }

    // ------------ 송신 ------------

    public Task<JsonElement> CommandAsync(params object[] args) => CommandAsync((IEnumerable<object>)args);

    public async Task<JsonElement> CommandAsync(IEnumerable<object> args)
    {
        var connection = Volatile.Read(ref _connection);
        if (connection is null || !connection.Pipe.IsConnected)
            throw new InvalidOperationException("mpv IPC not connected");

        var id = Interlocked.Increment(ref _nextRequestId);
        var tcs = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = new PendingRequest(connection.Generation, tcs);

        var payload = JsonSerializer.Serialize(new
        {
            command = args.ToArray(),
            request_id = id
        }, JsonOpts);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(4));
        var lockTaken = false;
        try
        {
            await _writeLock.WaitAsync(timeout.Token).ConfigureAwait(false);
            lockTaken = true;
            if (!ReferenceEquals(Volatile.Read(ref _connection), connection) ||
                !connection.Pipe.IsConnected)
                throw new IOException("mpv IPC connection changed before command write");

            await connection.Writer.WriteLineAsync(payload.AsMemory(), timeout.Token).ConfigureAwait(false);
            _writeLock.Release();
            lockTaken = false;
            return await tcs.Task.WaitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            throw new TimeoutException($"mpv command timeout: id={id}");
        }
        finally
        {
            if (lockTaken) _writeLock.Release();
            _pending.TryRemove(id, out _);
        }
    }

    /// <summary>fire-and-forget. 빈번한 seek 등에서 응답을 기다리지 않을 때.</summary>
    public async Task SendAsync(params object[] args)
        => _ = await TrySendAsync(args).ConfigureAwait(false);

    /// <summary>응답은 기다리지 않되 실제 pipe write 성공 여부는 호출자에게 돌려준다.</summary>
    public async Task<bool> TrySendAsync(params object[] args)
    {
        var connection = Volatile.Read(ref _connection);
        if (connection is null || !connection.Pipe.IsConnected) return false;
        var payload = JsonSerializer.Serialize(new { command = args }, JsonOpts);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var lockTaken = false;
        try
        {
            await _writeLock.WaitAsync(timeout.Token).ConfigureAwait(false);
            lockTaken = true;
            if (!ReferenceEquals(Volatile.Read(ref _connection), connection) ||
                !connection.Pipe.IsConnected)
                return false;
            await connection.Writer.WriteLineAsync(payload.AsMemory(), timeout.Token).ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (lockTaken) _writeLock.Release();
        }
    }

    /// <summary>
    /// 여러 fire-and-forget 명령을 한 번의 pipe lock/write/flush로 보낸다.
    /// mpv JSON IPC는 newline 단위 명령을 순서대로 처리하므로 transform처럼 한 프레임에
    /// 함께 바뀌는 속성들이 다른 빈번한 명령 사이에 끼거나 write backlog를 만들지 않는다.
    /// </summary>
    public async Task<bool> TrySendBatchAsync(params object[][] commands)
    {
        if (commands.Length == 0)
            return true;

        var connection = Volatile.Read(ref _connection);
        if (connection is null || !connection.Pipe.IsConnected) return false;

        var payload = string.Join('\n', commands.Select(args =>
            JsonSerializer.Serialize(new { command = args }, JsonOpts))) + "\n";
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var lockTaken = false;
        try
        {
            await _writeLock.WaitAsync(timeout.Token).ConfigureAwait(false);
            lockTaken = true;
            if (!ReferenceEquals(Volatile.Read(ref _connection), connection) ||
                !connection.Pipe.IsConnected)
                return false;

            await connection.Writer.WriteAsync(payload.AsMemory(), timeout.Token).ConfigureAwait(false);
            await connection.Writer.FlushAsync(timeout.Token).ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (lockTaken) _writeLock.Release();
        }
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
    // ab-loop: mpv가 a~b 구간 자동 루프. 편집 모드 IN/OUT 미리듣기.
    public Task SetAbLoop(double aSec, double bSec)
    {
        var t1 = SendAsync("set_property", "ab-loop-a", aSec);
        var t2 = SendAsync("set_property", "ab-loop-b", bSec);
        return Task.WhenAll(t1, t2);
    }
    public Task ClearAbLoop()
    {
        // "no" 문자열로 해제
        var t1 = SendAsync("set_property", "ab-loop-a", "no");
        var t2 = SendAsync("set_property", "ab-loop-b", "no");
        return Task.WhenAll(t1, t2);
    }
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
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        var connection = Interlocked.Exchange(ref _connection, null);
        if (connection is not null)
        {
            Interlocked.Exchange(ref connection.DisconnectNotified, 1);
            CloseConnection(connection);
            FailPending(connection.Generation, new ObjectDisposedException(nameof(MpvIpcClient)));
            connection.Cancellation.Dispose();
        }
        foreach (var pair in _pending)
        {
            if (_pending.TryRemove(pair.Key, out var pending))
                pending.Completion.TrySetException(new ObjectDisposedException(nameof(MpvIpcClient)));
        }
    }

    private async Task DisconnectCurrentAsync(bool suppressEvent)
    {
        var connection = Interlocked.Exchange(ref _connection, null);
        if (connection is null) return;
        if (suppressEvent) Interlocked.Exchange(ref connection.DisconnectNotified, 1);

        CloseConnection(connection);
        FailPending(connection.Generation, new IOException("mpv IPC connection replaced"));
        if (connection.ReadLoop is not null)
        {
            try
            {
                await connection.ReadLoop.WaitAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
            }
            catch { }
        }
        if (!suppressEvent) NotifyDisconnected(connection);
        connection.Cancellation.Dispose();
    }

    private static void CloseConnection(Connection connection)
    {
        try { connection.Cancellation.Cancel(); } catch { }
        try { connection.Writer.Dispose(); } catch { }
        try { connection.Reader.Dispose(); } catch { }
        try { connection.Pipe.Dispose(); } catch { }
    }

    private void FailPending(long generation, Exception error)
    {
        foreach (var pair in _pending)
        {
            if (pair.Value.Generation != generation) continue;
            if (_pending.TryRemove(pair.Key, out var pending))
                pending.Completion.TrySetException(error);
        }
    }

    private void NotifyDisconnected(Connection connection)
    {
        if (Interlocked.Exchange(ref connection.DisconnectNotified, 1) == 0)
            InvokeSafely(Disconnected);
    }

    private static void InvokeSafely(Action? handlers)
    {
        if (handlers is null) return;
        foreach (Action handler in handlers.GetInvocationList())
        {
            try { handler(); } catch { }
        }
    }

    private static void InvokeSafely<T>(Action<T>? handlers, T value)
    {
        if (handlers is null) return;
        foreach (Action<T> handler in handlers.GetInvocationList())
        {
            try { handler(value); } catch { }
        }
    }

    private static void InvokeSafely<T1, T2>(Action<T1, T2>? handlers, T1 first, T2 second)
    {
        if (handlers is null) return;
        foreach (Action<T1, T2> handler in handlers.GetInvocationList())
        {
            try { handler(first, second); } catch { }
        }
    }
}

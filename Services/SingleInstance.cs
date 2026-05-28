using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace DenoVideoPlayer.Services;

/// <summary>
/// 한 사용자 세션에 Deno Video Player 인스턴스를 하나만 유지. 두 번째 실행은
/// 첫 인스턴스에 명령줄 인자를 named pipe로 전달하고 자기 자신은 종료한다.
/// </summary>
internal sealed class SingleInstance : IDisposable
{
    private const string MutexName = @"Local\DenoVideoPlayer.SingleInstance.Mutex";
    private const string PipeName  =          "DenoVideoPlayer.SingleInstance.Pipe";

    private Mutex? _mutex;
    private bool _ownsMutex;
    private CancellationTokenSource? _cts;
    private Task? _serverLoop;

    /// <summary>
    /// 다른 인스턴스가 보낸 인자를 받는다. UI 스레드 마샬링은 호출자 책임.
    /// 구독자가 아직 없을 때 들어온 인자는 _pending에 보관 → 구독 시점에 drain.
    /// </summary>
    public event Action<string[]>? ArgsReceived
    {
        add
        {
            lock (_sync)
            {
                _argsReceived += value;
                if (value is not null && _pending.Count > 0)
                {
                    var backlog = _pending.ToArray();
                    _pending.Clear();
                    foreach (var a in backlog) { try { value(a); } catch { } }
                }
            }
        }
        remove { lock (_sync) { _argsReceived -= value; } }
    }
    private Action<string[]>? _argsReceived;
    private readonly object _sync = new();
    private readonly List<string[]> _pending = new();

    /// <returns>true면 우리가 첫 인스턴스(서버 시작 OK). false면 인자 전달 후 종료해야 함.</returns>
    public bool TryClaim(string[] args)
    {
        _mutex = new Mutex(initiallyOwned: false, MutexName, out var createdNew);
        try
        {
            _ownsMutex = _mutex.WaitOne(TimeSpan.Zero, false);
        }
        catch (AbandonedMutexException)
        {
            _ownsMutex = true; // 이전 인스턴스가 비정상 종료했음
        }

        if (!_ownsMutex)
        {
            // 우리는 두 번째 인스턴스 — 첫 인스턴스에 인자 전달
            try { SendArgs(args); } catch { /* 첫 인스턴스가 응답 없어도 그냥 종료 */ }
            return false;
        }

        StartServer();
        return true;
    }

    private void StartServer()
    {
        _cts = new CancellationTokenSource();
        _serverLoop = Task.Run(() => ServerLoopAsync(_cts.Token));
    }

    private async Task ServerLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            NamedPipeServerStream? server = null;
            try
            {
                server = new NamedPipeServerStream(
                    PipeName, PipeDirection.In, 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                await server.WaitForConnectionAsync(ct).ConfigureAwait(false);

                using var reader = new StreamReader(server, new UTF8Encoding(false));
                var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(line))
                {
                    string[]? args = null;
                    try { args = JsonSerializer.Deserialize<string[]>(line); } catch { }
                    if (args is { Length: > 0 })
                    {
                        Action<string[]>? handler;
                        lock (_sync)
                        {
                            handler = _argsReceived;
                            if (handler is null) _pending.Add(args);
                        }
                        if (handler is not null) handler(args);
                    }
                }
            }
            catch (OperationCanceledException) { break; }
            catch { /* 다음 client 대기 */ }
            finally
            {
                try { server?.Dispose(); } catch { }
            }
        }
    }

    private static void SendArgs(string[] args)
    {
        using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out, PipeOptions.None);
        client.Connect(2000);
        using var writer = new StreamWriter(client, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };
        writer.WriteLine(JsonSerializer.Serialize(args));
    }

    public void Dispose()
    {
        try { _cts?.Cancel(); } catch { }
        try { _serverLoop?.Wait(500); } catch { }
        if (_ownsMutex)
        {
            try { _mutex?.ReleaseMutex(); } catch { }
        }
        _mutex?.Dispose();
        _mutex = null;
    }
}

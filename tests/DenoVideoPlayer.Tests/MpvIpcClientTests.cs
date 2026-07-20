using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using DenoVideoPlayer.Services;

namespace DenoVideoPlayer.Tests;

public class MpvIpcClientTests
{
    [Fact]
    public async Task CommandRoundTripReturnsResponseData()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var pipeName = "deno-ipc-test-" + Guid.NewGuid().ToString("N");
        using var server = CreateServer(pipeName);
        var serverTask = Task.Run(async () =>
        {
            await server.WaitForConnectionAsync(cancellationToken);
            using var reader = new StreamReader(server, new UTF8Encoding(false),
                detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
            using var writer = new StreamWriter(server, new UTF8Encoding(false),
                bufferSize: 1024, leaveOpen: true)
                { AutoFlush = true, NewLine = "\n" };
            var line = await reader.ReadLineAsync();
            using var request = JsonDocument.Parse(line!);
            var requestId = request.RootElement.GetProperty("request_id").GetInt64();
            await writer.WriteLineAsync($"{{\"request_id\":{requestId},\"error\":\"success\",\"data\":42}}");
        }, cancellationToken);

        using var client = new MpvIpcClient();
        Assert.True(await client.ConnectAsync(pipeName, TimeSpan.FromSeconds(2), cancellationToken));
        var result = await client.CommandAsync("get_property", "volume");
        Assert.Equal(42, result.GetInt32());
        await serverTask;
    }

    [Fact]
    public async Task DisconnectFailsPendingCommandPromptly()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var pipeName = "deno-ipc-test-" + Guid.NewGuid().ToString("N");
        using var server = CreateServer(pipeName);
        var serverTask = Task.Run(async () =>
        {
            await server.WaitForConnectionAsync(cancellationToken);
            using var reader = new StreamReader(server, new UTF8Encoding(false),
                detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
            _ = await reader.ReadLineAsync();
            server.Disconnect();
        }, cancellationToken);

        using var client = new MpvIpcClient();
        Assert.True(await client.ConnectAsync(pipeName, TimeSpan.FromSeconds(2), cancellationToken));
        var stopwatch = Stopwatch.StartNew();
        await Assert.ThrowsAsync<IOException>(() => client.CommandAsync("get_property", "volume"));
        stopwatch.Stop();
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2), $"disconnect took {stopwatch.Elapsed}");
        await serverTask;
    }

    [Fact]
    public async Task ReconnectKeepsOldReadLoopFromDisconnectingNewGeneration()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var firstPipeName = "deno-ipc-test-" + Guid.NewGuid().ToString("N");
        var secondPipeName = "deno-ipc-test-" + Guid.NewGuid().ToString("N");
        using var firstServer = CreateServer(firstPipeName);
        using var secondServer = CreateServer(secondPipeName);
        var firstAccepted = firstServer.WaitForConnectionAsync(cancellationToken);

        using var client = new MpvIpcClient();
        var disconnected = 0;
        client.Disconnected += () => Interlocked.Increment(ref disconnected);

        Assert.True(await client.ConnectAsync(
            firstPipeName, TimeSpan.FromSeconds(2), cancellationToken));
        await firstAccepted;

        var secondAccepted = secondServer.WaitForConnectionAsync(cancellationToken);
        Assert.True(await client.ConnectAsync(
            secondPipeName, TimeSpan.FromSeconds(2), cancellationToken));
        await secondAccepted;
        await Task.Delay(100, cancellationToken);

        Assert.True(client.IsConnected);
        Assert.Equal(0, Volatile.Read(ref disconnected));
    }

    [Fact]
    public async Task TransformBatchWritesOrderedNewlineDelimitedCommands()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var pipeName = "deno-ipc-test-" + Guid.NewGuid().ToString("N");
        using var server = CreateServer(pipeName);
        var serverTask = Task.Run(async () =>
        {
            await server.WaitForConnectionAsync(cancellationToken);
            using var reader = new StreamReader(server, new UTF8Encoding(false),
                detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
            return new[]
            {
                await reader.ReadLineAsync(cancellationToken),
                await reader.ReadLineAsync(cancellationToken),
                await reader.ReadLineAsync(cancellationToken),
            };
        }, cancellationToken);

        using var client = new MpvIpcClient();
        Assert.True(await client.ConnectAsync(pipeName, TimeSpan.FromSeconds(2), cancellationToken));
        Assert.True(await client.TrySendBatchAsync(
            ["set_property", "video-zoom", 1.25],
            ["set_property", "video-pan-x", -0.1],
            ["set_property", "video-pan-y", 0.2]));

        var lines = await serverTask;
        var expectedProperties = new[] { "video-zoom", "video-pan-x", "video-pan-y" };
        for (var i = 0; i < lines.Length; i++)
        {
            Assert.NotNull(lines[i]);
            using var document = JsonDocument.Parse(lines[i]!);
            var command = document.RootElement.GetProperty("command");
            Assert.Equal("set_property", command[0].GetString());
            Assert.Equal(expectedProperties[i], command[1].GetString());
        }
    }

    private static NamedPipeServerStream CreateServer(string pipeName) =>
        new(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
}

using DenoVideoPlayer.Services;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace DenoVideoPlayer.Tests;

public class TrimServiceTests
{
    [Theory]
    [InlineData("clip.mp4", ".mka")]
    [InlineData("clip.mov", ".mka")]
    [InlineData("clip.mkv", ".mka")]
    [InlineData("clip.avi", ".mka")]
    [InlineData("clip.webm", ".mka")]
    [InlineData("song.mp3", ".mp3")]
    [InlineData("voice.opus", ".opus")]
    public void DefaultAudioExtensionMatchesSourceContainer(string inputPath, string expected)
    {
        Assert.Equal(expected, TrimService.DefaultAudioExtensionFor(inputPath));
    }

    [Theory]
    [InlineData("clip.mp4", "aac", ".m4a")]
    [InlineData("clip.mov", "alac", ".m4a")]
    [InlineData("clip.mp4", "mp3", ".mka")]
    [InlineData("clip.mov", "pcm_s16le", ".mka")]
    [InlineData("clip.mkv", "aac", ".mka")]
    public void DefaultAudioExtensionUsesCodecCompatibility(
        string inputPath, string codec, string expected)
    {
        Assert.Equal(expected, TrimService.DefaultAudioExtensionFor(inputPath, codec));
    }

    [Fact]
    public async Task RejectsInvalidRangeBeforeLookingForFfmpeg()
    {
        var input = Path.GetTempFileName();
        try
        {
            var result = await TrimService.TrimAsync(
                input,
                double.NaN,
                5,
                ct: TestContext.Current.CancellationToken);
            Assert.False(result.Success);
            Assert.Contains("OUT", result.Error);
        }
        finally
        {
            File.Delete(input);
        }
    }

    [Fact]
    public async Task FailedStreamCopyPreservesExistingDestinationWhenLocalFfmpegIsAvailable()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var ffmpeg = RuntimePaths.FfmpegExe;
        Assert.SkipUnless(
            RuntimeExecutableValidator.IsUsable(ffmpeg, "-version"),
            "Local Deno Video Player FFmpeg runtime is not installed.");

        var directory = Path.Combine(
            Path.GetTempPath(),
            "DenoVideoPlayerTrimTest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var input = Path.Combine(directory, "pcm-source.mov");
            var output = Path.Combine(directory, "existing-output.m4a");
            var sentinel = Encoding.UTF8.GetBytes("existing-user-file");
            await File.WriteAllBytesAsync(output, sentinel, cancellationToken);

            var createExitCode = await RunProcessAsync(
                ffmpeg,
                new[]
                {
                    "-y", "-f", "lavfi", "-i", "anullsrc=r=44100:cl=mono",
                    "-t", "1", "-c:a", "pcm_s16le", input
                },
                cancellationToken);
            Assert.Equal(0, createExitCode);

            var result = await TrimService.TrimAsync(
                input,
                0,
                0.5,
                output,
                TrimOutputMode.AudioOnly,
                cancellationToken);

            Assert.False(result.Success);
            Assert.Equal(sentinel, await File.ReadAllBytesAsync(output, cancellationToken));
            Assert.DoesNotContain(
                Directory.EnumerateFiles(directory),
                path => Path.GetFileName(path).Contains(".partial", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task SuccessfulStreamCopyReplacesExistingDestinationWhenLocalFfmpegIsAvailable()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var ffmpeg = RuntimePaths.FfmpegExe;
        Assert.SkipUnless(
            RuntimeExecutableValidator.IsUsable(ffmpeg, "-version"),
            "Local Deno Video Player FFmpeg runtime is not installed.");

        var directory = Path.Combine(
            Path.GetTempPath(),
            "DenoVideoPlayerTrimTest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var input = Path.Combine(directory, "aac-source.mp4");
            var output = Path.Combine(directory, "existing-output.m4a");
            var sentinel = Encoding.UTF8.GetBytes("existing-user-file");
            await File.WriteAllBytesAsync(output, sentinel, cancellationToken);

            var createExitCode = await RunProcessAsync(
                ffmpeg,
                new[]
                {
                    "-y", "-f", "lavfi", "-i", "sine=frequency=1000:sample_rate=44100",
                    "-t", "1", "-c:a", "aac", input
                },
                cancellationToken);
            Assert.Equal(0, createExitCode);

            var result = await TrimService.TrimAsync(
                input,
                0,
                0.5,
                output,
                TrimOutputMode.AudioOnly,
                cancellationToken);

            Assert.True(result.Success, result.Error);
            var exported = await File.ReadAllBytesAsync(output, cancellationToken);
            Assert.True(exported.Length > sentinel.Length);
            Assert.NotEqual(sentinel, exported);
            Assert.DoesNotContain(
                Directory.EnumerateFiles(directory),
                path => Path.GetFileName(path).Contains(".partial", StringComparison.OrdinalIgnoreCase) ||
                        Path.GetFileName(path).Contains(".backup", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task<int> RunProcessAsync(
        string executable,
        IEnumerable<string> arguments,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start FFmpeg test process.");
        var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
            }
            catch { }
            throw;
        }
        await Task.WhenAll(stdout, stderr);
        return process.ExitCode;
    }
}

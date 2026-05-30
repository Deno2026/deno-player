using DenoVideoPlayer.Services;

namespace DenoVideoPlayer.Tests;

public class RuntimePathsTests
{
    [Fact]
    public void RuntimeCacheUsesStableLocalAppDataLocation()
    {
        Assert.Contains("DenoVideoPlayer", RuntimePaths.RuntimeRoot);
        Assert.EndsWith(@"runtime\mpv\mpv.exe", RuntimePaths.MpvExe, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(@"runtime\ffmpeg\ffmpeg.exe", RuntimePaths.FfmpegExe, StringComparison.OrdinalIgnoreCase);
    }
}

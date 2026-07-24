using System.IO;
using DenoVideoPlayer.Services;
using DenoVideoPlayer.ViewModels;

namespace DenoVideoPlayer.Tests;

public sealed class UserSafetyRegressionTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(),
        "DenoVideoPlayerSafetyTests_" + Guid.NewGuid().ToString("N"));

    public UserSafetyRegressionTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    [Fact]
    public void RecentCleanupRemovesOnlyConfirmedLocalMissingPaths()
    {
        var existing = Path.Combine(_dir, "existing.mp4");
        File.WriteAllText(existing, "test");
        var missing = Path.Combine(_dir, "missing.mp4");

        Assert.False(RecentPathService.IsConfirmedMissing(existing));
        Assert.True(RecentPathService.IsConfirmedMissing(missing));
        Assert.False(RecentPathService.IsConfirmedMissing(
            @"\\offline.example.invalid\share\clip.mp4"));
        Assert.True(RecentPathService.IsConfirmedMissing(" "));
    }

    [Fact]
    public void ScreenshotPathsRemainUniqueWithinTheSameMillisecond()
    {
        var timestamp = new DateTime(2026, 7, 23, 12, 34, 56, 789);

        var first = ScreenshotPathService.ReserveUniquePngPath(
            _dir,
            "render:preview.mp4",
            timestamp);
        var second = ScreenshotPathService.ReserveUniquePngPath(
            _dir,
            "render:preview.mp4",
            timestamp);

        Assert.NotEqual(first, second);
        Assert.EndsWith(".png", first, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(".png", second, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(':', Path.GetFileName(first));
        Assert.Contains("20260723_123456_789", Path.GetFileName(first));

        ScreenshotPathService.ReleaseReservation(first);
        var reusable = ScreenshotPathService.ReserveUniquePngPath(
            _dir,
            "render:preview.mp4",
            timestamp);
        Assert.Equal(first, reusable);

        ScreenshotPathService.ReleaseReservation(second);
        ScreenshotPathService.ReleaseReservation(reusable);
    }

    [Fact]
    public void LegacyRuntimeSourcesStayInsideTheCurrentAppBody()
    {
        var appRoot = Path.GetFullPath(AppContext.BaseDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var mpv = Path.GetFullPath(RuntimePaths.LegacyBundledMpvDirectory);
        var ffmpeg = Path.GetFullPath(RuntimePaths.LegacyBundledFfmpegDirectory);

        Assert.StartsWith(appRoot, mpv, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(appRoot, ffmpeg, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(@"runtime\mpv\mpv.exe",
            RuntimePaths.LegacyBundledMpvExe,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FailureActionsDependOnFailureCauseInsteadOfMediaPresence()
    {
        Assert.True(PlayerFailurePolicy.CanRetryBackend(PlayerFailureKind.Backend));
        Assert.False(PlayerFailurePolicy.CanOpenAnotherFile(PlayerFailureKind.Backend));
        Assert.False(PlayerFailurePolicy.CanGoNext(PlayerFailureKind.Backend, hasMedia: true));

        Assert.False(PlayerFailurePolicy.CanRetryBackend(PlayerFailureKind.Media));
        Assert.True(PlayerFailurePolicy.CanOpenAnotherFile(PlayerFailureKind.Media));
        Assert.False(PlayerFailurePolicy.CanGoNext(PlayerFailureKind.Media, hasMedia: false));
        Assert.True(PlayerFailurePolicy.CanGoNext(PlayerFailureKind.Media, hasMedia: true));
    }

    [Theory]
    [InlineData(PlayerState.Failed, PlayerFailureKind.Backend, true, false)]
    [InlineData(PlayerState.Failed, PlayerFailureKind.Backend, false, false)]
    [InlineData(PlayerState.NoFile, PlayerFailureKind.None, false, false)]
    [InlineData(PlayerState.Failed, PlayerFailureKind.Media, true, true)]
    [InlineData(PlayerState.Playing, PlayerFailureKind.None, true, true)]
    public void PlaybackCommandsStayBlockedUntilTheBackendIsReady(
        PlayerState state,
        PlayerFailureKind failureKind,
        bool backendConnected,
        bool expected)
    {
        Assert.Equal(expected, PlayerFailurePolicy.CanStartMediaPlayback(
            state,
            failureKind,
            backendConnected));
    }

    [Theory]
    [InlineData(PlayerState.NoFile, false, PlayerDoubleClickAction.OpenFile)]
    [InlineData(PlayerState.NoFile, true, PlayerDoubleClickAction.OpenFile)]
    [InlineData(PlayerState.Loading, false, PlayerDoubleClickAction.None)]
    [InlineData(PlayerState.Loading, true, PlayerDoubleClickAction.None)]
    [InlineData(PlayerState.Dragging, true, PlayerDoubleClickAction.None)]
    [InlineData(PlayerState.Failed, false, PlayerDoubleClickAction.None)]
    [InlineData(PlayerState.Failed, true, PlayerDoubleClickAction.None)]
    [InlineData(PlayerState.Ready, true, PlayerDoubleClickAction.ToggleFullscreen)]
    [InlineData(PlayerState.Playing, true, PlayerDoubleClickAction.ToggleFullscreen)]
    [InlineData(PlayerState.Paused, true, PlayerDoubleClickAction.ToggleFullscreen)]
    public void DoubleClickActionRespectsPlayerState(
        PlayerState state,
        bool hasMedia,
        PlayerDoubleClickAction expected)
    {
        Assert.Equal(expected,
            PlayerInteractionPolicy.DoubleClickAction(state, hasMedia));
    }

    [Fact]
    public void AutoPlayNextStopsOnlyOrdinaryLinearAdvance()
    {
        Assert.False(PlaybackEndPolicy.AllowsLinearAdvance(
            autoPlayNext: false,
            RepeatMode.None));
        Assert.True(PlaybackEndPolicy.AllowsLinearAdvance(
            autoPlayNext: true,
            RepeatMode.None));
        Assert.True(PlaybackEndPolicy.AllowsLinearAdvance(
            autoPlayNext: false,
            RepeatMode.RepeatAll));
    }
}

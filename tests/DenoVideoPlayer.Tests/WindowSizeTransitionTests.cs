using DenoVideoPlayer.ViewModels;

namespace DenoVideoPlayer.Tests;

public sealed class WindowSizeTransitionTests
{
    [Theory]
    [InlineData(false, false, WindowSizeTransition.EnterFullscreen)]
    [InlineData(false, true, WindowSizeTransition.RestoreWindow)]
    [InlineData(true, false, WindowSizeTransition.ExitFullscreen)]
    [InlineData(true, true, WindowSizeTransition.ExitFullscreen)]
    public void ToggleRestoresEnlargedWindowsOrEntersFullscreen(
        bool isFullscreen,
        bool isMaximized,
        WindowSizeTransition expected)
    {
        Assert.Equal(expected, FullscreenWindowPolicy.Toggle(isFullscreen, isMaximized));
    }
}

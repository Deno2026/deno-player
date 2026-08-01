using DenoVideoPlayer.ViewModels;

namespace DenoVideoPlayer.Tests;

public sealed class FullscreenWindowPolicyTests
{
    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(true, false, false, false)]
    [InlineData(true, true, false, true)]
    [InlineData(false, false, true, true)]
    [InlineData(false, true, true, true)]
    [InlineData(true, false, true, true)]
    [InlineData(true, true, true, true)]
    public void TopmostFollowsFullscreenFocusOrUserPreference(
        bool isFullscreen,
        bool isApplicationForeground,
        bool isAlwaysOnTop,
        bool expected)
    {
        Assert.Equal(expected, FullscreenWindowPolicy.ShouldBeTopmost(
            isFullscreen,
            isApplicationForeground,
            isAlwaysOnTop));
    }
}

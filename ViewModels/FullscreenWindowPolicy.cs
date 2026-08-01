namespace DenoVideoPlayer.ViewModels;

public static class FullscreenWindowPolicy
{
    public static bool ShouldBeTopmost(
        bool isFullscreen,
        bool isApplicationForeground,
        bool isAlwaysOnTop) =>
        isAlwaysOnTop || (isFullscreen && isApplicationForeground);
}

namespace DenoVideoPlayer.ViewModels;

public enum WindowSizeTransition
{
    EnterFullscreen,
    ExitFullscreen,
    RestoreWindow
}

public static class FullscreenWindowPolicy
{
    public static WindowSizeTransition Toggle(bool isFullscreen, bool isMaximized) =>
        isFullscreen
            ? WindowSizeTransition.ExitFullscreen
            : isMaximized
                ? WindowSizeTransition.RestoreWindow
                : WindowSizeTransition.EnterFullscreen;

    public static bool ShouldBeTopmost(
        bool isFullscreen,
        bool isApplicationForeground,
        bool isAlwaysOnTop) =>
        isAlwaysOnTop || (isFullscreen && isApplicationForeground);
}

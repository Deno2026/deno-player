namespace DenoVideoPlayer.ViewModels;

public enum RepeatMode
{
    None = 0,
    RepeatAll = 1,
    RepeatOne = 2
}

public static class PlaybackEndPolicy
{
    public static bool AllowsLinearAdvance(bool autoPlayNext, RepeatMode repeat) =>
        autoPlayNext || repeat != RepeatMode.None;
}

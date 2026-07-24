namespace DenoVideoPlayer.ViewModels;

public enum PlayerState
{
    NoFile,         // 처음 상태 — drop/open 안내
    Dragging,       // 드래그 오버 중
    Loading,        // mpv가 파일을 로드 중
    Ready,          // 로드 완료 첫 프레임 대기
    Playing,
    Paused,
    Failed          // 로드 실패 / 지원 안 함
}

public enum PlayerFailureKind
{
    None,
    Backend,
    Media
}

public static class PlayerFailurePolicy
{
    public static bool CanRetryBackend(PlayerFailureKind kind) =>
        kind == PlayerFailureKind.Backend;

    public static bool CanOpenAnotherFile(PlayerFailureKind kind) =>
        kind == PlayerFailureKind.Media;

    public static bool CanGoNext(PlayerFailureKind kind, bool hasMedia) =>
        kind == PlayerFailureKind.Media && hasMedia;

    public static bool CanStartMediaPlayback(
        PlayerState state,
        PlayerFailureKind kind,
        bool backendConnected) =>
        backendConnected &&
        !(state == PlayerState.Failed && kind == PlayerFailureKind.Backend);
}

public enum PlayerDoubleClickAction
{
    None,
    OpenFile,
    ToggleFullscreen
}

public static class PlayerInteractionPolicy
{
    public static PlayerDoubleClickAction DoubleClickAction(
        PlayerState state,
        bool hasMedia)
    {
        if (state == PlayerState.NoFile)
            return PlayerDoubleClickAction.OpenFile;
        if (!hasMedia)
            return PlayerDoubleClickAction.None;

        return state is PlayerState.Ready or PlayerState.Playing or PlayerState.Paused
            ? PlayerDoubleClickAction.ToggleFullscreen
            : PlayerDoubleClickAction.None;
    }
}

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

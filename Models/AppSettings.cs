namespace DenoVideoPlayer.Models;

/// <summary>
/// 단일 설정 파일. 사용자 노출 UI 없음(MVP).
/// 위치: %APPDATA%\DenoVideoPlayer\settings.json
/// </summary>
public sealed class AppSettings
{
    public double WindowWidth { get; set; } = 1280;
    public double WindowHeight { get; set; } = 760;
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public bool WindowMaximized { get; set; } = true;   // 첫 실행은 풀스크린 화면 가까이 — 보고 끄는 도구

    public int Volume { get; set; } = 70;          // 0..100
    public bool Muted { get; set; } = false;
    public double PlaybackRate { get; set; } = 1.0;

    public string? LastOpenedFolder { get; set; }
    public bool AutoPlayNext { get; set; } = true;

    public int ControlAutoHideMs { get; set; } = 2500;
    public bool PlaylistPanelEnabled { get; set; } = true;
    public bool AlwaysOnTop { get; set; } = false;

    // 0=None, 1=RepeatAll, 2=RepeatOne
    public int RepeatMode { get; set; } = 0;
    public bool Shuffle { get; set; } = false;

    // 환경설정에서 사용자가 등록한 확장자 — null/빈이면 전체 default 사용
    public List<string>? RegisteredExtensions { get; set; }

    // 최근 재생 기록 (최신 → 오래된 순). 최대 30개 유지.
    public List<string>? RecentFiles { get; set; }

    // 스크린샷 저장 폴더. null/빈이면 기본 Pictures\Deno Video Player\.
    public string? ScreenshotFolder { get; set; }

    public static AppSettings Defaults() => new();
}

namespace DenoPlayer.Models;

/// <summary>
/// 단일 설정 파일. 사용자 노출 UI 없음(MVP).
/// 위치: %APPDATA%\DenoPlayer\settings.json
/// </summary>
public sealed class AppSettings
{
    public double WindowWidth { get; set; } = 1280;
    public double WindowHeight { get; set; } = 760;
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public bool WindowMaximized { get; set; }

    public int Volume { get; set; } = 70;          // 0..100
    public bool Muted { get; set; } = false;
    public double PlaybackRate { get; set; } = 1.0;

    public string? LastOpenedFolder { get; set; }
    public bool AutoPlayNext { get; set; } = true;

    public int ControlAutoHideMs { get; set; } = 2500;
    public bool PlaylistPanelEnabled { get; set; } = true;
    public bool AlwaysOnTop { get; set; } = false;

    public static AppSettings Defaults() => new();
}

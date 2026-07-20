namespace DenoVideoPlayer.Models;

/// <summary>
/// 단일 설정 파일. 사용자 노출 UI 없음(MVP).
/// 위치: %APPDATA%\DenoVideoPlayer\settings.json
/// </summary>
public sealed class AppSettings
{
    private const double MinWindowWidth = 480;
    private const double MinWindowHeight = 320;
    private const double MaxWindowDimension = 16384;
    private const int MinControlAutoHideMs = 250;
    private const int MaxControlAutoHideMs = 60000;
    private const int MaxRecentFiles = 30;

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

    // 표시 언어: "ko" 또는 "en".
    public string Language { get; set; } = "ko";

    // 이전 설정 파일 호환용. 현재 fullscreen 영상 전용 모드는 명시 버튼으로 전환한다.
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

    /// <summary>
    /// settings.json은 사용자가 직접 수정하거나 이전 버전에서 넘어올 수 있으므로,
    /// UI/타이머/mpv에 적용하기 전에 앱이 안전하게 처리할 수 있는 범위로 정규화한다.
    /// </summary>
    public AppSettings Normalize()
    {
        var defaults = Defaults();
        WindowWidth = NormalizeFinite(WindowWidth, MinWindowWidth, MaxWindowDimension, defaults.WindowWidth);
        WindowHeight = NormalizeFinite(WindowHeight, MinWindowHeight, MaxWindowDimension, defaults.WindowHeight);
        WindowLeft = NormalizeOptionalFinite(WindowLeft);
        WindowTop = NormalizeOptionalFinite(WindowTop);
        Volume = Math.Clamp(Volume, 0, 100);
        PlaybackRate = NormalizeFinite(PlaybackRate, 0.25, 4.0, defaults.PlaybackRate);
        Language = string.Equals(Language, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "ko";
        ControlAutoHideMs = Math.Clamp(ControlAutoHideMs, MinControlAutoHideMs, MaxControlAutoHideMs);
        RepeatMode = Math.Clamp(RepeatMode, 0, 2);

        RegisteredExtensions = NormalizeExtensions(RegisteredExtensions);
        RecentFiles = NormalizeRecentFiles(RecentFiles);
        LastOpenedFolder = NormalizeOptionalText(LastOpenedFolder);
        ScreenshotFolder = NormalizeOptionalText(ScreenshotFolder);
        return this;
    }

    private static double NormalizeFinite(double value, double min, double max, double fallback)
        => double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;

    private static double? NormalizeOptionalFinite(double? value)
        => value is { } number && double.IsFinite(number) ? number : null;

    private static string? NormalizeOptionalText(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;

    private static List<string>? NormalizeExtensions(IEnumerable<string>? extensions)
    {
        if (extensions is null) return null;

        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in extensions)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var ext = raw.Trim();
            if (!ext.StartsWith(".", StringComparison.Ordinal)) ext = "." + ext;
            if (ext.Length is < 2 or > 16 || ext.Skip(1).Any(c => !char.IsLetterOrDigit(c))) continue;
            ext = ext.ToLowerInvariant();
            if (seen.Add(ext)) result.Add(ext);
        }
        return result;
    }

    private static List<string>? NormalizeRecentFiles(IEnumerable<string>? paths)
    {
        if (paths is null) return null;

        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path) || !seen.Add(path)) continue;
            result.Add(path);
            if (result.Count >= MaxRecentFiles) break;
        }
        return result;
    }
}

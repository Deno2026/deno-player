using System.IO;

namespace DenoVideoPlayer.Models;

public enum MediaKind
{
    Unknown,
    Video,
    Audio,
    Image
}

public static class MediaKindExtensions
{
    // 단일 출처: 모든 확장자 판단은 여기로 모은다.
    private static readonly HashSet<string> VideoExt = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mkv", ".mov", ".webm", ".avi", ".m4v", ".ts", ".mts", ".m2ts", ".wmv", ".flv", ".3gp"
    };

    private static readonly HashSet<string> AudioExt = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".wav", ".flac", ".aac", ".m4a", ".mka", ".ogg", ".opus", ".wma", ".alac"
    };

    private static readonly HashSet<string> ImageExt = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".bmp", ".gif"
    };

    /// <summary>드롭 시 자막으로 인식하는 확장자. 새 미디어가 아니라 현재 영상에 add-sub.</summary>
    private static readonly HashSet<string> SubtitleExt = new(StringComparer.OrdinalIgnoreCase)
    {
        ".srt", ".ass", ".ssa", ".vtt", ".sub", ".idx", ".sup", ".smi"
    };

    public static MediaKind FromPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return MediaKind.Unknown;
        var ext = Path.GetExtension(path);
        if (VideoExt.Contains(ext)) return MediaKind.Video;
        if (AudioExt.Contains(ext)) return MediaKind.Audio;
        if (ImageExt.Contains(ext)) return MediaKind.Image;
        return MediaKind.Unknown;
    }

    public static bool IsSupported(string path) => FromPath(path) != MediaKind.Unknown;
    public static bool IsSubtitle(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        return SubtitleExt.Contains(Path.GetExtension(path));
    }

    public static IEnumerable<string> AllSupportedExtensions()
        => VideoExt.Concat(AudioExt).Concat(ImageExt);
}

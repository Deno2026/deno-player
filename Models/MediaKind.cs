using System.IO;

namespace DenoPlayer.Models;

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
        ".mp3", ".wav", ".flac", ".aac", ".m4a", ".ogg", ".opus", ".wma", ".alac"
    };

    private static readonly HashSet<string> ImageExt = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".bmp", ".gif"
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

    public static IEnumerable<string> AllSupportedExtensions()
        => VideoExt.Concat(AudioExt).Concat(ImageExt);
}

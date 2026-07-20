using System.IO;
using DenoVideoPlayer.Helpers;
using DenoVideoPlayer.Models;

namespace DenoVideoPlayer.Services;

/// <summary>
/// "현재 파일이 있는 폴더만 스캔". DB/썸네일/색인 금지.
/// 생성시간 최신순으로 같은 폴더의 지원 미디어 모음.
/// </summary>
public sealed class PlaylistService
{
    private sealed record FileSnapshot(string Path, DateTime CreationTimeUtc);

    public static IComparer<string> CreationTimeDescendingPathComparer { get; } =
        Comparer<string>.Create(ComparePathsByCreationTimeDescending);

    public List<MediaItem> BuildFromFile(string filePath)
    {
        var list = new List<MediaItem>();
        if (string.IsNullOrWhiteSpace(filePath)) return list;
        // CLI 인자/드롭에서 forward-slash로 들어올 수 있어 GetFullPath로 백슬래시 정규화.
        // 정규화 안 하면 EnumerateFiles(폴더, ...) 결과와 시드 경로가 string equals 비교에서
        // 다르게 보여 시드 파일을 못 찾고 list.Insert(0, ...)으로 중복 등록되는 버그.
        try { filePath = Path.GetFullPath(filePath); } catch { /* invalid path 무시 */ }
        var folder = Path.GetDirectoryName(filePath);
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
        {
            if (File.Exists(filePath)) list.Add(new MediaItem(filePath));
            return list;
        }

        // 시드 파일의 kind만 재생목록에 포함 — 비디오 클릭하면 비디오만, 음악 → 음악만,
        // 이미지 → 이미지만. ComfyUI 폴더처럼 같은 이름의 .mp4 옆에 .png 미리보기가
        // 섞여있어도 깔끔하게 영상만 보임.
        var seedKind = MediaKindExtensions.FromPath(filePath);
        foreach (var p in GetPlayableFilesSorted(folder))
        {
            if (MediaKindExtensions.FromPath(p) == seedKind)
                list.Add(new MediaItem(p));
        }

        if (!list.Any(m => string.Equals(m.FullPath, filePath, StringComparison.OrdinalIgnoreCase))
            && File.Exists(filePath))
        {
            list.Insert(0, new MediaItem(filePath));
        }

        return list;
    }

    public IReadOnlyList<string> GetPlayableFilesSorted(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            return Array.Empty<string>();

        var files = new List<FileSnapshot>();
        try
        {
            // EnumerateFiles는 지연 열거이므로 foreach 자체를 try 안에 둬야
            // 분리 드라이브/네트워크 폴더가 도중에 끊겨도 UI까지 예외가 새지 않는다.
            foreach (var path in Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly))
            {
                if (!MediaKindExtensions.IsSupported(path)) continue;
                files.Add(new FileSnapshot(path, GetCreationTimeUtc(path)));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Warn($"Playlist enumeration stopped: {directory}: {ex.Message}");
        }

        // creation time은 항목당 한 번만 읽고 메모리 snapshot을 정렬한다.
        files.Sort(CompareSnapshotsByCreationTimeDescending);
        return files.Select(file => file.Path).ToArray();
    }

    public string? FindFirstPlayableFile(string directory)
        => GetPlayableFilesSorted(directory).FirstOrDefault();

    private static int CompareSnapshotsByCreationTimeDescending(FileSnapshot? left, FileSnapshot? right)
    {
        if (ReferenceEquals(left, right)) return 0;
        if (left is null) return 1;
        if (right is null) return -1;

        var timeCompare = right.CreationTimeUtc.CompareTo(left.CreationTimeUtc);
        if (timeCompare != 0) return timeCompare;

        var nameCompare = NaturalStringComparer.Instance.Compare(
            Path.GetFileName(left.Path), Path.GetFileName(right.Path));
        if (nameCompare != 0) return nameCompare;
        return string.Compare(left.Path, right.Path, StringComparison.OrdinalIgnoreCase);
    }

    private static int ComparePathsByCreationTimeDescending(string? left, string? right)
    {
        var timeCompare = GetCreationTimeUtc(right).CompareTo(GetCreationTimeUtc(left));
        if (timeCompare != 0) return timeCompare;

        var nameCompare = NaturalStringComparer.Instance.Compare(
            Path.GetFileName(left ?? ""),
            Path.GetFileName(right ?? ""));
        if (nameCompare != 0) return nameCompare;

        return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private static DateTime GetCreationTimeUtc(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return DateTime.MinValue;
        try { return File.GetCreationTimeUtc(path); }
        catch { return DateTime.MinValue; }
    }

    public int IndexOf(IList<MediaItem> list, string path)
    {
        for (var i = 0; i < list.Count; i++)
            if (string.Equals(list[i].FullPath, path, StringComparison.OrdinalIgnoreCase))
                return i;
        return -1;
    }
}

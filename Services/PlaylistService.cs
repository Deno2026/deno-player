using System.IO;
using DenoVideoPlayer.Helpers;
using DenoVideoPlayer.Models;

namespace DenoVideoPlayer.Services;

public sealed record PlaylistBuildResult(string SeedPath, IReadOnlyList<MediaItem> Items);

/// <summary>
/// "현재 파일이 있는 폴더만 스캔". DB/썸네일/색인 금지.
/// 사용자가 선택한 기준으로 같은 폴더의 지원 미디어 모음.
/// </summary>
public sealed class PlaylistService
{
    private sealed record FileSnapshot(string Path, DateTime CreationTimeUtc);
    private sealed record MediaSnapshot(MediaItem Item, DateTime CreationTimeUtc);

    public List<MediaItem> BuildFromFile(
        string filePath,
        PlaylistSortMode sortMode = PlaylistSortMode.NameAscending)
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
        foreach (var p in GetPlayableFilesSorted(folder, sortMode))
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

    public IReadOnlyList<string> GetPlayableFilesSorted(
        string directory,
        PlaylistSortMode sortMode = PlaylistSortMode.NameAscending)
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
                files.Add(new FileSnapshot(
                    path,
                    sortMode == PlaylistSortMode.NameAscending
                        ? DateTime.MinValue
                        : GetCreationTimeUtc(path)));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Warn($"Playlist enumeration stopped: {directory}: {ex.Message}");
        }

        // 기본 이름순은 추가 metadata I/O 없이 정렬한다. 날짜순을 고른 경우에도
        // 생성 시각은 항목당 한 번만 읽고 메모리 snapshot을 정렬한다.
        files.Sort((left, right) => CompareSnapshots(left, right, sortMode));
        return files.Select(file => file.Path).ToArray();
    }

    /// <summary>
    /// 폴더를 한 번만 스캔해 정렬상 첫 파일과 그 파일과 같은 kind의 목록을 함께 만든다.
    /// 폴더 열기에서 FindFirst + BuildFromFile로 같은 metadata를 두 번 읽지 않게 한다.
    /// </summary>
    public PlaylistBuildResult? BuildFromDirectory(
        string directory,
        PlaylistSortMode sortMode = PlaylistSortMode.NameAscending)
    {
        var sortedPaths = GetPlayableFilesSorted(directory, sortMode);
        var seedPath = sortedPaths.FirstOrDefault();
        if (seedPath is null) return null;

        var seedKind = MediaKindExtensions.FromPath(seedPath);
        var items = sortedPaths
            .Where(path => MediaKindExtensions.FromPath(path) == seedKind)
            .Select(path => new MediaItem(path))
            .ToArray();
        return new PlaylistBuildResult(seedPath, items);
    }

    public string? FindFirstPlayableFile(
        string directory,
        PlaylistSortMode sortMode = PlaylistSortMode.NameAscending)
        => GetPlayableFilesSorted(directory, sortMode).FirstOrDefault();

    /// <summary>
    /// 이미 재생 중인 MediaItem 객체는 교체하지 않고 순서만 계산한다.
    /// 날짜 metadata 읽기는 호출자가 UI thread 밖에서 실행할 수 있게 동기 API로 둔다.
    /// </summary>
    public IReadOnlyList<MediaItem> SortItems(
        IEnumerable<MediaItem> items,
        PlaylistSortMode sortMode)
    {
        var snapshots = items.Select(item => new MediaSnapshot(
            item,
            sortMode == PlaylistSortMode.NameAscending
                ? DateTime.MinValue
                : GetCreationTimeUtc(item.FullPath))).ToList();

        snapshots.Sort((left, right) => Compare(
            left.Item.FullPath,
            left.CreationTimeUtc,
            right.Item.FullPath,
            right.CreationTimeUtc,
            sortMode));
        return snapshots.Select(snapshot => snapshot.Item).ToArray();
    }

    private static int CompareSnapshots(
        FileSnapshot? left,
        FileSnapshot? right,
        PlaylistSortMode sortMode)
    {
        if (ReferenceEquals(left, right)) return 0;
        if (left is null) return 1;
        if (right is null) return -1;

        return Compare(
            left.Path,
            left.CreationTimeUtc,
            right.Path,
            right.CreationTimeUtc,
            sortMode);
    }

    private static int Compare(
        string leftPath,
        DateTime leftCreationUtc,
        string rightPath,
        DateTime rightCreationUtc,
        PlaylistSortMode sortMode)
    {
        var timeCompare = sortMode switch
        {
            PlaylistSortMode.CreatedDescending => rightCreationUtc.CompareTo(leftCreationUtc),
            PlaylistSortMode.CreatedAscending => leftCreationUtc.CompareTo(rightCreationUtc),
            _ => 0
        };
        if (timeCompare != 0) return timeCompare;

        var nameCompare = NaturalStringComparer.Instance.Compare(
            Path.GetFileName(leftPath), Path.GetFileName(rightPath));
        if (nameCompare != 0) return nameCompare;
        return string.Compare(leftPath, rightPath, StringComparison.OrdinalIgnoreCase);
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

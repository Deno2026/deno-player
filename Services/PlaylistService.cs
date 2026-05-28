using System.IO;
using DenoVideoPlayer.Helpers;
using DenoVideoPlayer.Models;

namespace DenoVideoPlayer.Services;

/// <summary>
/// "현재 파일이 있는 폴더만 스캔". DB/썸네일/색인 금지.
/// 자연 정렬로 같은 폴더의 지원 미디어 모음.
/// </summary>
public sealed class PlaylistService
{
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

        IEnumerable<string> paths;
        try
        {
            paths = Directory.EnumerateFiles(folder, "*", SearchOption.TopDirectoryOnly);
        }
        catch
        {
            if (File.Exists(filePath)) list.Add(new MediaItem(filePath));
            return list;
        }

        // 시드 파일의 kind만 재생목록에 포함 — 비디오 클릭하면 비디오만, 음악 → 음악만,
        // 이미지 → 이미지만. ComfyUI 폴더처럼 같은 이름의 .mp4 옆에 .png 미리보기가
        // 섞여있어도 깔끔하게 영상만 보임.
        var seedKind = MediaKindExtensions.FromPath(filePath);
        foreach (var p in paths)
        {
            if (MediaKindExtensions.FromPath(p) == seedKind)
                list.Add(new MediaItem(p));
        }

        list.Sort((a, b) => NaturalStringComparer.Instance.Compare(a.FileName, b.FileName));

        if (!list.Any(m => string.Equals(m.FullPath, filePath, StringComparison.OrdinalIgnoreCase))
            && File.Exists(filePath))
        {
            list.Insert(0, new MediaItem(filePath));
        }

        return list;
    }

    public int IndexOf(IList<MediaItem> list, string path)
    {
        for (var i = 0; i < list.Count; i++)
            if (string.Equals(list[i].FullPath, path, StringComparison.OrdinalIgnoreCase))
                return i;
        return -1;
    }
}

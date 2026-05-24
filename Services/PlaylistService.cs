using System.IO;
using DenoPlayer.Helpers;
using DenoPlayer.Models;

namespace DenoPlayer.Services;

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
        var folder = Path.GetDirectoryName(filePath);
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
        {
            // 폴더가 없으면 단일 파일만이라도 재생목록에 둔다.
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

        foreach (var p in paths.Where(MediaKindExtensions.IsSupported))
            list.Add(new MediaItem(p));

        list.Sort((a, b) => NaturalStringComparer.Instance.Compare(a.FileName, b.FileName));

        // 호출자가 준 파일이 누락되었으면(권한·경합) 강제 추가.
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

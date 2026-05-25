using System.IO;

namespace DenoPlayer.Models;

public sealed class RecentItem
{
    public string FullPath { get; }
    public string FileName { get; }
    public string FolderDisplay { get; }
    public MediaKind Kind { get; }

    public RecentItem(string path)
    {
        FullPath = path;
        FileName = Path.GetFileName(path);
        FolderDisplay = Path.GetDirectoryName(path) ?? "";
        Kind = MediaKindExtensions.FromPath(path);
    }
}

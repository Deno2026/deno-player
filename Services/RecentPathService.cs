using System.IO;

namespace DenoVideoPlayer.Services;

public static class RecentPathService
{
    /// <summary>
    /// Returns true only when a missing path is on a currently available local volume.
    /// Disconnected removable drives, mapped network drives, and UNC paths are retained
    /// so a temporary disconnect does not erase the user's recent-file history.
    /// </summary>
    public static bool IsConfirmedMissing(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return true;

        try
        {
            var fullPath = Path.GetFullPath(path);
            if (fullPath.StartsWith(@"\\", StringComparison.Ordinal)) return false;

            try
            {
                _ = File.GetAttributes(fullPath);
                return false;
            }
            catch (FileNotFoundException)
            {
                // The volume check below distinguishes a deleted local file
                // from a temporarily disconnected removable drive.
            }
            catch (DirectoryNotFoundException)
            {
                // Same as above. A missing parent can still mean offline media.
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }

            var root = Path.GetPathRoot(fullPath);
            if (string.IsNullOrWhiteSpace(root)) return true;

            var drive = new DriveInfo(root);
            if (drive.DriveType == DriveType.Network || !drive.IsReady) return false;
            return true;
        }
        catch
        {
            // An availability probe must never turn an uncertain path into a deletion.
            return false;
        }
    }
}

using System.Globalization;
using System.IO;

namespace DenoVideoPlayer.Services;

public static class ScreenshotPathService
{
    private static readonly object ReservationGate = new();
    private static readonly HashSet<string> ReservedPaths =
        new(StringComparer.OrdinalIgnoreCase);

    public static string ReserveUniquePngPath(
        string directory,
        string mediaFileName,
        DateTime localTimestamp)
    {
        var raw = Path.GetFileNameWithoutExtension(mediaFileName);
        var invalid = Path.GetInvalidFileNameChars();
        var safe = string.Concat(raw.Where(c => !invalid.Contains(c)));
        if (string.IsNullOrWhiteSpace(safe)) safe = "screenshot";

        var stamp = localTimestamp.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);
        var stem = $"{safe}_{stamp}";

        lock (ReservationGate)
        {
            for (var suffix = 0; ; suffix++)
            {
                var name = suffix == 0 ? $"{stem}.png" : $"{stem}_{suffix + 1}.png";
                var path = Path.Combine(directory, name);
                if (File.Exists(path) || !ReservedPaths.Add(path)) continue;
                return path;
            }
        }
    }

    public static void ReleaseReservation(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        lock (ReservationGate)
            ReservedPaths.Remove(path);
    }
}

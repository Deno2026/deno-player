using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;

namespace DenoVideoPlayer.Services;

/// <summary>
/// 외부 runtime의 단순 존재 여부가 아니라 최소 PE 형식과 실제 version 실행을 확인한다.
/// 파일 메타데이터가 같을 때만 결과를 재사용해 정상 실행마다 프로세스를 반복 생성하지 않는다.
/// </summary>
public static class RuntimeExecutableValidator
{
    private sealed record CacheEntry(long Length, DateTime LastWriteUtc, bool Usable);

    private static readonly ConcurrentDictionary<string, CacheEntry> Cache =
        new(StringComparer.OrdinalIgnoreCase);

    public static bool IsUsable(string path, string versionArgument, int timeoutMs = 3000)
    {
        if (!HasPlausiblePortableExecutableHeader(path, out var info)) return false;

        var fullPath = Path.GetFullPath(path);
        if (Cache.TryGetValue(fullPath, out var cached) &&
            cached.Length == info.Length && cached.LastWriteUtc == info.LastWriteTimeUtc)
            return cached.Usable;

        var usable = TryRunVersion(fullPath, versionArgument, timeoutMs);
        Cache[fullPath] = new CacheEntry(info.Length, info.LastWriteTimeUtc, usable);
        return usable;
    }

    public static bool HasPlausiblePortableExecutableHeader(string path)
        => HasPlausiblePortableExecutableHeader(path, out _);

    public static void Invalidate(string path)
    {
        try { Cache.TryRemove(Path.GetFullPath(path), out _); }
        catch { }
    }

    private static bool HasPlausiblePortableExecutableHeader(string path, out FileInfo info)
    {
        info = null!;
        try
        {
            info = new FileInfo(path);
            if (!info.Exists || info.Length < 4096) return false;
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            return stream.ReadByte() == 'M' && stream.ReadByte() == 'Z';
        }
        catch
        {
            return false;
        }
    }

    private static bool TryRunVersion(string path, string versionArgument, int timeoutMs)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            psi.ArgumentList.Add(versionArgument);

            using var process = Process.Start(psi);
            if (process is null) return false;
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(timeoutMs))
            {
                TryKillAndWait(process);
                return false;
            }

            Task.WaitAll(new Task[] { stdout, stderr }, Math.Min(1000, timeoutMs));
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static void TryKillAndWait(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            process.WaitForExit(1000);
        }
        catch { }
    }
}

using System.Diagnostics;
using System.IO;
using System.Text;

namespace DenoVideoPlayer.Services;

/// <summary>
/// First-run preparation for external runtime tools that are intentionally not
/// redistributed in the public installer package.
/// </summary>
public static class RuntimeDependencyService
{
    public sealed record EnsureResult(bool Success, string? Error = null);

    public static Task<EnsureResult> EnsureMpvAsync(CancellationToken ct = default)
        => EnsureMpvAsync(null, ct);

    public static Task<EnsureResult> EnsureMpvAsync(Action<string>? outputLine, CancellationToken ct = default)
    {
        PreserveExistingRuntimeCache();
        return RunFetcherAsync(
            "mpv",
            Path.Combine(AppContext.BaseDirectory, "tools", "fetch-mpv.ps1"),
            RuntimePaths.MpvDirectory,
            TimeSpan.FromMinutes(8),
            ct,
            outputLine);
    }

    public static Task<EnsureResult> EnsureFfmpegAsync(CancellationToken ct = default)
    {
        PreserveExistingRuntimeCache();
        return RunFetcherAsync(
            "ffmpeg",
            Path.Combine(AppContext.BaseDirectory, "tools", "fetch-ffmpeg.ps1"),
            RuntimePaths.FfmpegDirectory,
            TimeSpan.FromMinutes(8),
            ct);
    }

    public static void PreserveExistingRuntimeCache()
    {
        PromoteRuntimeDirectory("mpv", "mpv.exe", RuntimePaths.MpvDirectory);
        PromoteRuntimeDirectory("ffmpeg", "ffmpeg.exe", RuntimePaths.FfmpegDirectory);
    }

    private static async Task<EnsureResult> RunFetcherAsync(
        string name, string scriptPath, string destinationDir, TimeSpan timeout, CancellationToken ct,
        Action<string>? outputLine = null)
    {
        if (!File.Exists(scriptPath))
            return new EnsureResult(false, $"필요한 준비 스크립트를 찾을 수 없습니다: {scriptPath}");

        var psi = new ProcessStartInfo
        {
            FileName = ResolvePowerShell(),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = AppContext.BaseDirectory,
        };
        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-ExecutionPolicy");
        psi.ArgumentList.Add("Bypass");
        psi.ArgumentList.Add("-File");
        psi.ArgumentList.Add(scriptPath);
        psi.ArgumentList.Add("-Dest");
        psi.ArgumentList.Add(destinationDir);
        psi.ArgumentList.Add("-SkipIfExists");

        AppLog.Info($"Runtime prepare: {name} via {scriptPath} -> {destinationDir}");

        using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var output = new StringBuilder();
        var error = new StringBuilder();
        proc.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            output.AppendLine(e.Data);
            outputLine?.Invoke(e.Data);
        };
        proc.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            error.AppendLine(e.Data);
            outputLine?.Invoke(e.Data);
        };

        try
        {
            if (!proc.Start())
                return new EnsureResult(false, $"{name} 준비 프로세스를 시작하지 못했습니다.");

            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(timeout);
            await proc.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);

            if (proc.ExitCode == 0)
            {
                AppLog.Info($"Runtime prepare ok: {name}");
                return new EnsureResult(true);
            }

            var details = TailLines(error.Length > 0 ? error.ToString() : output.ToString(), 8);
            AppLog.Warn($"Runtime prepare failed: {name} exit={proc.ExitCode}\n{details}");
            return new EnsureResult(false, $"{name} 준비 실패\n{details}");
        }
        catch (OperationCanceledException)
        {
            TryKill(proc);
            if (ct.IsCancellationRequested)
            {
                AppLog.Info($"Runtime prepare canceled: {name}");
                return new EnsureResult(false, $"{name} 준비가 취소되었습니다.");
            }

            AppLog.Warn($"Runtime prepare timeout: {name}");
            return new EnsureResult(false, $"{name} 다운로드 시간이 초과되었습니다. 인터넷 연결을 확인한 뒤 다시 실행하세요.");
        }
        catch (Exception ex)
        {
            TryKill(proc);
            AppLog.Error($"Runtime prepare crashed: {name}", ex);
            return new EnsureResult(false, ex.Message);
        }
    }

    private static string ResolvePowerShell()
    {
        var systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var ps = Path.Combine(systemRoot, "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
        return File.Exists(ps) ? ps : "powershell.exe";
    }

    private static void PromoteRuntimeDirectory(string name, string exeName, string destinationDir)
    {
        if (File.Exists(Path.Combine(destinationDir, exeName))) return;

        foreach (var sourceDir in RuntimeDirectoryCandidates(name))
        {
            if (!Directory.Exists(sourceDir)) continue;
            var sourceExe = Path.Combine(sourceDir, exeName);
            if (!File.Exists(sourceExe)) continue;

            try
            {
                Directory.CreateDirectory(destinationDir);
                foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.TopDirectoryOnly))
                {
                    var dest = Path.Combine(destinationDir, Path.GetFileName(file));
                    File.Copy(file, dest, overwrite: true);
                }

                AppLog.Info($"Runtime cache promoted: {name} {sourceDir} -> {destinationDir}");
                return;
            }
            catch (Exception ex)
            {
                AppLog.Warn($"Runtime cache promote failed: {name} {sourceDir} -> {destinationDir}: {ex.Message}");
            }
        }
    }

    private static IEnumerable<string> RuntimeDirectoryCandidates(string name)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var baseDir = AppContext.BaseDirectory;
        foreach (var dir in ParentRuntimeDirectories(baseDir, name))
        {
            if (!dir.StartsWith(RuntimePaths.RuntimeRoot, StringComparison.OrdinalIgnoreCase) && seen.Add(dir))
                yield return dir;
        }
    }

    private static IEnumerable<string> ParentRuntimeDirectories(string baseDir, string name)
    {
        var probe = baseDir;
        for (var i = 0; i < 6 && probe is not null; i++)
        {
            yield return Path.Combine(probe, "runtime", name);
            probe = Path.GetDirectoryName(probe);
        }
    }

    private static void TryKill(Process proc)
    {
        try
        {
            if (!proc.HasExited) proc.Kill(entireProcessTree: true);
        }
        catch { }
    }

    private static string TailLines(string value, int count)
    {
        var lines = value.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var start = Math.Max(0, lines.Length - count);
        return string.Join('\n', lines[start..]).Trim();
    }
}

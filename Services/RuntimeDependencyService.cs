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
        => RunFetcherAsync("mpv", Path.Combine(AppContext.BaseDirectory, "tools", "fetch-mpv.ps1"),
            TimeSpan.FromMinutes(8), ct);

    public static Task<EnsureResult> EnsureFfmpegAsync(CancellationToken ct = default)
        => RunFetcherAsync("ffmpeg", Path.Combine(AppContext.BaseDirectory, "tools", "fetch-ffmpeg.ps1"),
            TimeSpan.FromMinutes(8), ct);

    private static async Task<EnsureResult> RunFetcherAsync(
        string name, string scriptPath, TimeSpan timeout, CancellationToken ct)
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
        psi.ArgumentList.Add("-SkipIfExists");

        AppLog.Info($"Runtime prepare: {name} via {scriptPath}");

        using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var output = new StringBuilder();
        var error = new StringBuilder();
        proc.OutputDataReceived += (_, e) => { if (e.Data is not null) output.AppendLine(e.Data); };
        proc.ErrorDataReceived += (_, e) => { if (e.Data is not null) error.AppendLine(e.Data); };

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

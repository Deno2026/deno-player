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
    public sealed record EnsureResult(
        bool Success,
        string? Error = null,
        LocalizedText? LocalizedError = null)
    {
        public object? ErrorDetail =>
            LocalizedError is not null ? LocalizedError : Error;
    }

    public static Task<EnsureResult> EnsureMpvAsync(CancellationToken ct = default)
        => EnsureMpvAsync(null, ct);

    public static Task<EnsureResult> EnsureMpvAsync(Action<string>? outputLine, CancellationToken ct = default)
    {
        PreserveExistingMpvCache();
        return RunFetcherAsync(
            "mpv",
            Path.Combine(AppContext.BaseDirectory, "tools", "fetch-mpv.ps1"),
            RuntimePaths.MpvDirectory,
            RuntimePaths.MpvExe,
            "--version",
            TimeSpan.FromMinutes(8),
            ct,
            outputLine);
    }

    public static Task<EnsureResult> EnsureFfmpegAsync(CancellationToken ct = default)
        => EnsureFfmpegAsync(null, ct);

    public static Task<EnsureResult> EnsureFfmpegAsync(
        Action<string>? outputLine,
        CancellationToken ct = default)
    {
        PreserveExistingFfmpegCache();
        return RunFetcherAsync(
            "ffmpeg",
            Path.Combine(AppContext.BaseDirectory, "tools", "fetch-ffmpeg.ps1"),
            RuntimePaths.FfmpegDirectory,
            RuntimePaths.FfmpegExe,
            "-version",
            TimeSpan.FromMinutes(8),
            ct,
            outputLine);
    }

    public static void PreserveExistingRuntimeCache()
    {
        PreserveExistingMpvCache();
        PreserveExistingFfmpegCache();
    }

    public static void PreserveExistingMpvCache()
        => PromoteRuntimeDirectory("mpv", "mpv.exe", RuntimePaths.MpvDirectory);

    public static void PreserveExistingFfmpegCache()
        => PromoteRuntimeDirectory("ffmpeg", "ffmpeg.exe", RuntimePaths.FfmpegDirectory);

    private static async Task<EnsureResult> RunFetcherAsync(
        string name, string scriptPath, string destinationDir,
        string expectedExecutable, string versionArgument,
        TimeSpan timeout, CancellationToken ct,
        Action<string>? outputLine = null)
    {
        if (!File.Exists(scriptPath))
            return LocalizedFailure("RuntimeScriptMissing", name, scriptPath);

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
        if (RuntimeExecutableValidator.IsUsable(expectedExecutable, versionArgument))
            psi.ArgumentList.Add("-SkipIfExists");

        AppLog.Info($"Runtime prepare: {name} via {scriptPath} -> {destinationDir}");

        using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var output = new StringBuilder();
        var error = new StringBuilder();
        proc.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            output.AppendLine(e.Data);
            try { outputLine?.Invoke(e.Data); }
            catch (Exception ex) { AppLog.Warn($"Runtime status callback failed: {ex.Message}"); }
        };
        proc.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            error.AppendLine(e.Data);
            try { outputLine?.Invoke(e.Data); }
            catch (Exception ex) { AppLog.Warn($"Runtime status callback failed: {ex.Message}"); }
        };

        try
        {
            if (!proc.Start())
                return LocalizedFailure("RuntimePrepareProcessStartFailed", name);

            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(timeout);
            await proc.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
            // 비동기 stdout/stderr 이벤트가 모두 전달될 때까지 한 번 더 동기화한다.
            proc.WaitForExit();

            if (proc.ExitCode == 0)
            {
                RuntimeExecutableValidator.Invalidate(expectedExecutable);
                if (!RuntimeExecutableValidator.IsUsable(expectedExecutable, versionArgument))
                {
                    AppLog.Warn($"Runtime prepare produced invalid executable: {expectedExecutable}");
                    return LocalizedFailure("RuntimeExecutableValidationFailed", name);
                }
                AppLog.Info($"Runtime prepare ok: {name}");
                return new EnsureResult(true);
            }

            var details = TailLines(error.Length > 0 ? error.ToString() : output.ToString(), 8);
            AppLog.Warn($"Runtime prepare failed: {name} exit={proc.ExitCode}\n{details}");
            return LocalizedFailure("RuntimePrepareDetailFailed", name, details);
        }
        catch (OperationCanceledException)
        {
            TryKill(proc);
            if (ct.IsCancellationRequested)
            {
                AppLog.Info($"Runtime prepare canceled: {name}");
                return LocalizedFailure("RuntimePrepareCanceled", name);
            }

            AppLog.Warn($"Runtime prepare timeout: {name}");
            return LocalizedFailure("RuntimePrepareTimeout", name);
        }
        catch (Exception ex)
        {
            TryKill(proc);
            AppLog.Error($"Runtime prepare crashed: {name}", ex);
            return new EnsureResult(false, ex.Message);
        }
    }

    private static EnsureResult LocalizedFailure(
        string key,
        params object?[] args)
    {
        var detail = new LocalizedText(key, args);
        return new EnsureResult(
            Success: false,
            Error: detail.ToString(),
            LocalizedError: detail);
    }

    private static string ResolvePowerShell()
    {
        var systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var ps = Path.Combine(systemRoot, "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
        return File.Exists(ps) ? ps : "powershell.exe";
    }

    private static void PromoteRuntimeDirectory(string name, string exeName, string destinationDir)
    {
        var versionArgument = string.Equals(name, "mpv", StringComparison.OrdinalIgnoreCase)
            ? "--version"
            : "-version";
        var destinationExe = Path.Combine(destinationDir, exeName);
        // 정상 실행 hot path에서는 별도 --version process를 만들지 않는다. staged download와
        // legacy source promotion은 아래/RunFetcherAsync에서 계속 정밀 실행 검증한다.
        if (RuntimeExecutableValidator.HasPlausiblePortableExecutableHeader(destinationExe)) return;

        foreach (var sourceDir in RuntimeDirectoryCandidates(name))
        {
            if (!Directory.Exists(sourceDir)) continue;
            var sourceExe = Path.Combine(sourceDir, exeName);
            if (!RuntimeExecutableValidator.IsUsable(sourceExe, versionArgument)) continue;

            try
            {
                Directory.CreateDirectory(destinationDir);
                var files = Directory.EnumerateFiles(sourceDir, "*", SearchOption.TopDirectoryOnly).ToList();
                foreach (var file in files.Where(file =>
                             !string.Equals(Path.GetFileName(file), exeName, StringComparison.OrdinalIgnoreCase)))
                {
                    var dest = Path.Combine(destinationDir, Path.GetFileName(file));
                    AtomicCopy(file, dest);
                }
                // exe를 마지막에 교체한다. exe 존재가 runtime ready marker 역할을 하기 때문.
                AtomicCopy(sourceExe, destinationExe);
                RuntimeExecutableValidator.Invalidate(destinationExe);
                if (!RuntimeExecutableValidator.IsUsable(destinationExe, versionArgument))
                    throw new InvalidDataException($"Promoted {name} executable failed validation.");

                AppLog.Info($"Runtime cache promoted: {name} {sourceDir} -> {destinationDir}");
                return;
            }
            catch (Exception ex)
            {
                AppLog.Warn($"Runtime cache promote failed: {name} {sourceDir} -> {destinationDir}: {ex.Message}");
            }
        }
    }

    private static void AtomicCopy(string source, string destination)
    {
        var directory = Path.GetDirectoryName(destination)
            ?? throw new InvalidOperationException("Runtime destination directory is missing.");
        Directory.CreateDirectory(directory);
        var temp = Path.Combine(directory, $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.Copy(source, temp, overwrite: false);
            if (File.Exists(destination))
                File.Replace(temp, destination, destinationBackupFileName: null, ignoreMetadataErrors: true);
            else
                File.Move(temp, destination);
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch { }
        }
    }

    private static IEnumerable<string> RuntimeDirectoryCandidates(string name)
    {
        // Legacy promotion is intentionally limited to the currently running app body.
        // Ancestor probing can cross the app trust boundary and execute an unrelated PE.
        var bundled = Path.GetFullPath(
            string.Equals(name, "mpv", StringComparison.OrdinalIgnoreCase)
                ? RuntimePaths.LegacyBundledMpvDirectory
                : RuntimePaths.LegacyBundledFfmpegDirectory);
        var destination = Path.GetFullPath(Path.Combine(RuntimePaths.RuntimeRoot, name));
        if (!string.Equals(bundled, destination, StringComparison.OrdinalIgnoreCase))
            yield return bundled;
    }

    private static void TryKill(Process proc)
    {
        try
        {
            if (!proc.HasExited)
            {
                proc.Kill(entireProcessTree: true);
                proc.WaitForExit(1500);
            }
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

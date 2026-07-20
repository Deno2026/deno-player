using System.Diagnostics;
using System.IO;

namespace DenoVideoPlayer.Services;

public enum TrimOutputMode
{
    Clip,
    AudioOnly,
    VideoOnly
}

/// <summary>
/// ffmpeg 기반 단순 잘라내기. stream copy(-c copy)만 사용 — re-encode 없음, 무손실,
/// 거의 즉시. 단점: 키프레임 단위라 정확도가 0~수 초 단위로 떨어질 수 있음 (충분히
/// 정확한 cut이 필요하면 video 편집기 사용 권장 — 이 앱의 목적이 아님).
///
/// ffmpeg.exe 경로 우선순위:
/// 1) 사용자 고정 cache (%LOCALAPPDATA%\DenoVideoPlayer\runtime\ffmpeg)
/// 2) AppContext.BaseDirectory + runtime\ffmpeg\ffmpeg.exe (이전 설치/개발 호환)
/// 3) AppContext.BaseDirectory + runtime\mpv\ffmpeg.exe (mpv 빌드와 합본인 경우)
/// 4) PATH에서 ffmpeg (시스템 설치)
/// 5) 못 찾으면 명확한 에러 — 다운로드 안내.
/// </summary>
public static class TrimService
{
    private static readonly HashSet<string> AudioContainerExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".wav", ".flac", ".aac", ".m4a", ".ogg", ".opus", ".wma", ".alac", ".mka"
    };

    private static readonly HashSet<string> M4aCompatibleAudioCodecs = new(StringComparer.OrdinalIgnoreCase)
    {
        "aac", "alac"
    };

    public static string? FindFfmpeg()
    {
        RuntimeDependencyService.PreserveExistingRuntimeCache();

        var candidates = new[]
        {
            RuntimePaths.FfmpegExe,
            Path.Combine(AppContext.BaseDirectory, "runtime", "ffmpeg", "ffmpeg.exe"),
            Path.Combine(AppContext.BaseDirectory, "runtime", "mpv", "ffmpeg.exe"),
        };
        foreach (var c in candidates)
            if (RuntimeExecutableValidator.IsUsable(c, "-version")) return c;

        // PATH
        try
        {
            var pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (pathEnv is not null)
            {
                foreach (var dir in pathEnv.Split(Path.PathSeparator))
                {
                    try
                    {
                        var full = Path.Combine(dir, "ffmpeg.exe");
                        if (RuntimeExecutableValidator.IsUsable(full, "-version")) return full;
                    }
                    catch { /* invalid PATH entry */ }
                }
            }
        }
        catch { /* env read fail */ }

        return null;
    }

    public static string? FindFfprobe()
    {
        var candidates = new[]
        {
            RuntimePaths.FfprobeExe,
            Path.Combine(AppContext.BaseDirectory, "runtime", "ffmpeg", "ffprobe.exe"),
            Path.Combine(AppContext.BaseDirectory, "runtime", "mpv", "ffprobe.exe"),
        };
        foreach (var candidate in candidates)
            if (RuntimeExecutableValidator.IsUsable(candidate, "-version")) return candidate;

        try
        {
            foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
            {
                try
                {
                    var full = Path.Combine(dir, "ffprobe.exe");
                    if (RuntimeExecutableValidator.IsUsable(full, "-version")) return full;
                }
                catch { }
            }
        }
        catch { }
        return null;
    }

    public sealed record TrimResult(bool Success, string? OutputPath, string? Error);

    public static string DefaultAudioExtensionFor(string inputPath, string? audioCodec = null)
    {
        var ext = Path.GetExtension(inputPath).ToLowerInvariant();
        if (AudioContainerExtensions.Contains(ext)) return ext;

        return ext is ".mp4" or ".m4v" or ".mov" &&
               !string.IsNullOrWhiteSpace(audioCodec) &&
               M4aCompatibleAudioCodecs.Contains(audioCodec.Trim())
            ? ".m4a"
            : ".mka";
    }

    public static async Task<string> RecommendAudioExtensionAsync(
        string inputPath, CancellationToken ct = default)
    {
        var sourceExt = Path.GetExtension(inputPath).ToLowerInvariant();
        if (AudioContainerExtensions.Contains(sourceExt)) return sourceExt;

        var codec = await ProbeFirstAudioCodecAsync(inputPath, ct).ConfigureAwait(false);
        return DefaultAudioExtensionFor(inputPath, codec);
    }

    /// <summary>
    /// inputPath의 startSec ~ endSec 구간을 stream copy로 잘라 outputPath에 저장.
    /// outputPath 미지정 시 원본 폴더에 `<name>_clip_<start>-<end>.<ext>` 자동 생성.
    /// 동일 이름 존재하면 `_2`, `_3` suffix.
    /// </summary>
    public static async Task<TrimResult> TrimAsync(
        string inputPath, double startSec, double endSec, string? outputPath = null,
        TrimOutputMode outputMode = TrimOutputMode.Clip,
        CancellationToken ct = default)
    {
        if (!File.Exists(inputPath))
            return new TrimResult(false, null, $"원본 파일을 찾을 수 없습니다: {inputPath}");
        if (!double.IsFinite(startSec) || !double.IsFinite(endSec) || startSec < 0 || endSec <= startSec)
            return new TrimResult(false, null, "OUT 지점이 IN 지점보다 뒤에 있어야 합니다.");
        if (!Enum.IsDefined(outputMode))
            return new TrimResult(false, null, "지원하지 않는 출력 방식입니다.");

        var ffmpeg = FindFfmpeg();
        if (ffmpeg is null)
            return new TrimResult(false, null,
                "ffmpeg.exe를 찾을 수 없습니다. runtime\\ffmpeg\\ffmpeg.exe를 두거나, " +
                "START_HERE.bat를 다시 실행해 자동 다운로드하세요.");

        outputPath ??= MakeOutputPath(inputPath, startSec, endSec, outputMode);
        string fullInput;
        string fullOutput;
        try
        {
            fullInput = Path.GetFullPath(inputPath);
            fullOutput = Path.GetFullPath(outputPath);
        }
        catch (Exception ex)
        {
            return new TrimResult(false, null, $"출력 경로가 올바르지 않습니다: {ex.Message}");
        }

        if (string.Equals(fullInput, fullOutput, StringComparison.OrdinalIgnoreCase))
            return new TrimResult(false, null, "원본 파일과 같은 경로에는 저장할 수 없습니다.");

        var outputDirectory = Path.GetDirectoryName(fullOutput);
        var outputExtension = Path.GetExtension(fullOutput);
        if (string.IsNullOrWhiteSpace(outputDirectory) || !Directory.Exists(outputDirectory))
            return new TrimResult(false, null, "출력 폴더를 찾을 수 없습니다.");
        if (string.IsNullOrWhiteSpace(outputExtension))
            return new TrimResult(false, null, "출력 파일 확장자가 필요합니다.");

        var tempOutput = Path.Combine(
            outputDirectory,
            $".{Path.GetFileNameWithoutExtension(fullOutput)}.{Guid.NewGuid():N}.partial{outputExtension}");
        var args = BuildFfmpegArgs(fullInput, startSec, endSec, tempOutput, outputMode);

        var psi = new ProcessStartInfo
        {
            FileName = ffmpeg,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        AppLog.Info($"Trim[{outputMode}]: {Path.GetFileName(fullInput)} [{startSec:0.00}~{endSec:0.00}] → {Path.GetFileName(fullOutput)}");
        Process? proc = null;
        try
        {
            ct.ThrowIfCancellationRequested();
            proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            if (!proc.Start())
                return new TrimResult(false, null, "ffmpeg 프로세스 시작 실패");

            var stderrTask = proc.StandardError.ReadToEndAsync();
            var stdoutTask = proc.StandardOutput.ReadToEndAsync();

            try
            {
                await proc.WaitForExitAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                TryKillAndWait(proc);
                await Task.WhenAll(stderrTask, stdoutTask).ConfigureAwait(false);
                return new TrimResult(false, null, "작업이 취소되었습니다.");
            }

            var stderr = await stderrTask.ConfigureAwait(false);
            await stdoutTask.ConfigureAwait(false);

            if (proc.ExitCode != 0)
            {
                var tail = TailLines(stderr, 6);
                AppLog.Warn($"Trim ffmpeg exit={proc.ExitCode}\n{tail}");
                return new TrimResult(false, null, $"ffmpeg 종료 코드 {proc.ExitCode}\n{tail}");
            }
            if (!File.Exists(tempOutput) || new FileInfo(tempOutput).Length == 0)
                return new TrimResult(false, null, "출력 파일이 생성되지 않았습니다.");

            CommitOutput(tempOutput, fullOutput);
            AppLog.Info($"Trim ok: {fullOutput} ({new FileInfo(fullOutput).Length:N0} bytes)");
            return new TrimResult(true, fullOutput, null);
        }
        catch (OperationCanceledException)
        {
            if (proc is not null) TryKillAndWait(proc);
            return new TrimResult(false, null, "작업이 취소되었습니다.");
        }
        catch (Exception ex)
        {
            if (proc is not null) TryKillAndWait(proc);
            AppLog.Error("TrimAsync", ex);
            return new TrimResult(false, null, ex.Message);
        }
        finally
        {
            proc?.Dispose();
            try { if (File.Exists(tempOutput)) File.Delete(tempOutput); } catch { }
        }
    }

    private static async Task<string?> ProbeFirstAudioCodecAsync(
        string inputPath, CancellationToken ct)
    {
        var ffprobe = FindFfprobe();
        if (ffprobe is null || !File.Exists(inputPath)) return null;

        var psi = new ProcessStartInfo
        {
            FileName = ffprobe,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var arg in new[]
                 {
                     "-v", "error", "-select_streams", "a:0",
                     "-show_entries", "stream=codec_name",
                     "-of", "default=noprint_wrappers=1:nokey=1", inputPath
                 })
            psi.ArgumentList.Add(arg);

        using var process = new Process { StartInfo = psi };
        try
        {
            if (!process.Start()) return null;
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            try
            {
                await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                TryKillAndWait(process);
                if (ct.IsCancellationRequested) throw;
                return null;
            }

            var stdout = await stdoutTask.ConfigureAwait(false);
            await stderrTask.ConfigureAwait(false);
            return process.ExitCode == 0 ? stdout.Trim() : null;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            TryKillAndWait(process);
            return null;
        }
    }

    private static void TryKillAndWait(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            process.WaitForExit(1500);
        }
        catch { }
    }

    private static void CommitOutput(string temporaryPath, string destinationPath)
    {
        if (!File.Exists(destinationPath))
        {
            File.Move(temporaryPath, destinationPath);
            return;
        }

        try
        {
            File.Replace(
                temporaryPath,
                destinationPath,
                destinationBackupFileName: null,
                ignoreMetadataErrors: true);
            return;
        }
        catch (Exception ex) when (ex is IOException or PlatformNotSupportedException)
        {
            // ReplaceFile을 지원하지 않는 외장 드라이브에서는 복구 가능한 rename 순서로 대체한다.
            AppLog.Warn($"Atomic output replace unavailable; using guarded fallback: {ex.Message}");
        }

        var backupPath = destinationPath + $".{Guid.NewGuid():N}.backup";
        File.Move(destinationPath, backupPath);
        try
        {
            File.Move(temporaryPath, destinationPath);
            try { File.Delete(backupPath); }
            catch (Exception ex) { AppLog.Warn($"Trim backup cleanup failed: {backupPath}: {ex.Message}"); }
        }
        catch
        {
            try
            {
                if (!File.Exists(destinationPath) && File.Exists(backupPath))
                    File.Move(backupPath, destinationPath);
            }
            catch (Exception restoreError)
            {
                AppLog.Error($"Trim destination restore failed; backup preserved at {backupPath}", restoreError);
            }
            throw;
        }
    }

    private static string[] BuildFfmpegArgs(
        string inputPath,
        double startSec,
        double endSec,
        string outputPath,
        TrimOutputMode outputMode)
    {
        // -ss before -i: fast seek. stream copy는 정확 컷보다 속도/무손실을 우선한다.
        var args = new List<string>
        {
            "-y",
            "-ss", startSec.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture),
            "-to", endSec.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture),
            "-i", inputPath,
        };

        switch (outputMode)
        {
            case TrimOutputMode.AudioOnly:
                args.AddRange(new[]
                {
                    "-map", "0:a:0",
                    "-vn", "-sn", "-dn",
                    "-c:a", "copy",
                    "-avoid_negative_ts", "make_zero",
                });
                break;

            case TrimOutputMode.VideoOnly:
                args.AddRange(new[]
                {
                    "-map", "0:v:0",
                    "-an", "-sn", "-dn",
                    "-c:v", "copy",
                    "-avoid_negative_ts", "make_zero",
                });
                break;

            default:
                args.AddRange(new[]
                {
                    "-map", "0",
                    "-c", "copy",
                    "-avoid_negative_ts", "make_zero",
                });
                break;
        }

        args.Add(outputPath);
        return args.ToArray();
    }

    private static string MakeOutputPath(string input, double inSec, double outSec, TrimOutputMode outputMode)
    {
        var dir = Path.GetDirectoryName(input) ?? Environment.CurrentDirectory;
        var name = Path.GetFileNameWithoutExtension(input);
        var ext  = outputMode == TrimOutputMode.AudioOnly
            ? DefaultAudioExtensionFor(input)
            : Path.GetExtension(input);
        var suffixName = outputMode switch
        {
            TrimOutputMode.AudioOnly => "audio",
            TrimOutputMode.VideoOnly => "video",
            _ => "clip"
        };
        string Make(int suffix) => Path.Combine(dir,
            suffix == 0
                ? $"{name}_{suffixName}_{FormatSec(inSec)}-{FormatSec(outSec)}{ext}"
                : $"{name}_{suffixName}_{FormatSec(inSec)}-{FormatSec(outSec)}_{suffix}{ext}");
        var i = 0;
        while (File.Exists(Make(i))) i++;
        return Make(i);
    }

    private static string FormatSec(double sec)
    {
        var t = TimeSpan.FromSeconds(sec);
        return $"{(int)t.TotalMinutes:00}m{t.Seconds:00}s";
    }

    private static string TailLines(string s, int n)
    {
        var lines = s.Split('\n');
        var start = Math.Max(0, lines.Length - n);
        return string.Join('\n', lines[start..]).Trim();
    }
}

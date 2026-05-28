using System.Diagnostics;
using System.IO;

namespace DenoVideoPlayer.Services;

/// <summary>
/// ffmpeg 기반 단순 잘라내기. stream copy(-c copy)만 사용 — re-encode 없음, 무손실,
/// 거의 즉시. 단점: 키프레임 단위라 정확도가 0~수 초 단위로 떨어질 수 있음 (충분히
/// 정확한 cut이 필요하면 video 편집기 사용 권장 — 이 앱의 목적이 아님).
///
/// ffmpeg.exe 경로 우선순위:
/// 1) AppContext.BaseDirectory + runtime\ffmpeg\ffmpeg.exe (앱 번들)
/// 2) AppContext.BaseDirectory + runtime\mpv\ffmpeg.exe (mpv 빌드와 합본인 경우)
/// 3) PATH에서 ffmpeg (시스템 설치)
/// 4) 못 찾으면 명확한 에러 — 다운로드 안내.
/// </summary>
public static class TrimService
{
    public static string? FindFfmpeg()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "runtime", "ffmpeg", "ffmpeg.exe"),
            Path.Combine(AppContext.BaseDirectory, "runtime", "mpv", "ffmpeg.exe"),
        };
        foreach (var c in candidates)
            if (File.Exists(c)) return c;

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
                        if (File.Exists(full)) return full;
                    }
                    catch { /* invalid PATH entry */ }
                }
            }
        }
        catch { /* env read fail */ }

        return null;
    }

    public sealed record TrimResult(bool Success, string? OutputPath, string? Error);

    /// <summary>
    /// inputPath의 startSec ~ endSec 구간을 stream copy로 잘라 outputPath에 저장.
    /// outputPath 미지정 시 원본 폴더에 `<name>_clip_<start>-<end>.<ext>` 자동 생성.
    /// 동일 이름 존재하면 `_2`, `_3` suffix.
    /// </summary>
    public static async Task<TrimResult> TrimAsync(
        string inputPath, double startSec, double endSec, string? outputPath = null,
        CancellationToken ct = default)
    {
        if (!File.Exists(inputPath))
            return new TrimResult(false, null, $"원본 파일을 찾을 수 없습니다: {inputPath}");
        if (endSec <= startSec)
            return new TrimResult(false, null, "OUT 지점이 IN 지점보다 뒤에 있어야 합니다.");

        var ffmpeg = FindFfmpeg();
        if (ffmpeg is null)
            return new TrimResult(false, null,
                "ffmpeg.exe를 찾을 수 없습니다. runtime\\ffmpeg\\ffmpeg.exe를 두거나, " +
                "START_HERE.bat를 다시 실행해 자동 다운로드하세요.");

        outputPath ??= MakeOutputPath(inputPath, startSec, endSec);

        // -ss before -i: fast seek (less accurate, but stream copy doesn't allow accurate seek anyway)
        // -avoid_negative_ts make_zero: cut 후 timestamp 0부터 시작하게 보정
        // -map 0: 모든 stream (video+audio+subtitle) 포함
        // -c copy: re-encode 없이 그대로 복사
        var args = new[]
        {
            "-y",
            "-ss", startSec.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture),
            "-to", endSec.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture),
            "-i", inputPath,
            "-map", "0",
            "-c", "copy",
            "-avoid_negative_ts", "make_zero",
            outputPath
        };

        var psi = new ProcessStartInfo
        {
            FileName = ffmpeg,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        AppLog.Info($"Trim: {Path.GetFileName(inputPath)} [{startSec:0.00}~{endSec:0.00}] → {Path.GetFileName(outputPath)}");
        try
        {
            using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            var stderrBuf = new System.Text.StringBuilder();
            proc.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderrBuf.AppendLine(e.Data); };
            if (!proc.Start())
                return new TrimResult(false, null, "ffmpeg 프로세스 시작 실패");
            proc.BeginErrorReadLine();

            await proc.WaitForExitAsync(ct).ConfigureAwait(false);

            if (proc.ExitCode != 0)
            {
                var tail = TailLines(stderrBuf.ToString(), 6);
                AppLog.Warn($"Trim ffmpeg exit={proc.ExitCode}\n{tail}");
                return new TrimResult(false, null, $"ffmpeg 종료 코드 {proc.ExitCode}\n{tail}");
            }
            if (!File.Exists(outputPath))
                return new TrimResult(false, null, "출력 파일이 생성되지 않았습니다.");

            AppLog.Info($"Trim ok: {outputPath} ({new FileInfo(outputPath).Length:N0} bytes)");
            return new TrimResult(true, outputPath, null);
        }
        catch (Exception ex)
        {
            AppLog.Error("TrimAsync", ex);
            return new TrimResult(false, null, ex.Message);
        }
    }

    private static string MakeOutputPath(string input, double inSec, double outSec)
    {
        var dir = Path.GetDirectoryName(input) ?? Environment.CurrentDirectory;
        var name = Path.GetFileNameWithoutExtension(input);
        var ext  = Path.GetExtension(input);
        string Make(int suffix) => Path.Combine(dir,
            suffix == 0
                ? $"{name}_clip_{FormatSec(inSec)}-{FormatSec(outSec)}{ext}"
                : $"{name}_clip_{FormatSec(inSec)}-{FormatSec(outSec)}_{suffix}{ext}");
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

using System.Diagnostics;
using System.IO;

namespace DenoPlayer.Services;

/// <summary>
/// runtime/mpv/mpv.exe를 호스트 윈도우 영역에 embed.
/// 별도 프로세스 + named pipe IPC. 앱 종료 시 확실히 kill.
/// </summary>
public sealed class MpvProcessService : IDisposable
{
    public string PipeName { get; }
    public Process? Process { get; private set; }
    public string MpvPath { get; }
    public event Action? Crashed;

    public MpvProcessService()
    {
        PipeName = $"deno-player-{Environment.ProcessId}-{Guid.NewGuid():N}".Substring(0, 40);
        MpvPath = ResolveMpvPath();
    }

    /// <summary>mpv.exe 위치: 1) 앱과 같은 폴더 runtime\mpv\mpv.exe → 2) 프로젝트 루트 runtime\mpv\mpv.exe</summary>
    private static string ResolveMpvPath()
    {
        var baseDir = AppContext.BaseDirectory;
        var p1 = Path.Combine(baseDir, "runtime", "mpv", "mpv.exe");
        if (File.Exists(p1)) return p1;

        // dev 실행 시 bin\Debug\net8.0-windows\... 에서 프로젝트 루트 추적
        var probe = baseDir;
        for (var i = 0; i < 6 && probe is not null; i++)
        {
            var alt = Path.Combine(probe, "runtime", "mpv", "mpv.exe");
            if (File.Exists(alt)) return alt;
            probe = Path.GetDirectoryName(probe);
        }
        return p1; // 없어도 경로는 반환(에러는 호출자가 처리)
    }

    public bool MpvAvailable => File.Exists(MpvPath);

    public void Start(IntPtr videoHostHwnd)
    {
        if (Process is { HasExited: false }) return;
        if (!MpvAvailable)
            throw new FileNotFoundException(
                $"mpv.exe를 찾을 수 없습니다.\n경로: {MpvPath}\nREADME의 'mpv 설치' 섹션을 참고하세요.",
                MpvPath);

        var pipePath = $@"\\.\pipe\{PipeName}";
        var psi = new ProcessStartInfo
        {
            FileName = MpvPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
            WorkingDirectory = Path.GetDirectoryName(MpvPath) ?? AppContext.BaseDirectory,
        };

        void Arg(string s) => psi.ArgumentList.Add(s);
        Arg("--no-config");
        Arg("--idle=yes");
        Arg("--force-window=yes");
        Arg("--terminal=no");
        Arg("--osc=no");
        Arg("--no-osd-bar");
        Arg("--input-default-bindings=no");
        Arg("--input-vo-keyboard=no");
        Arg("--keep-open=yes");
        Arg("--image-display-duration=inf");
        Arg("--hr-seek=yes");
        Arg("--cache=yes");
        Arg("--background=color");
        Arg("--background-color=#0B0E0C");
        Arg("--osd-font='Segoe UI'");
        Arg($"--input-ipc-server={pipePath}");
        Arg($"--wid={videoHostHwnd.ToInt64()}");

        var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("mpv 프로세스를 시작하지 못했습니다.");
        proc.EnableRaisingEvents = true;
        proc.Exited += (_, _) =>
        {
            // 호스트 윈도우가 살아있는데 mpv가 죽으면 crash 신호
            Crashed?.Invoke();
        };
        Process = proc;
    }

    public void Dispose()
    {
        try
        {
            if (Process is { HasExited: false })
            {
                Process.Kill(entireProcessTree: true);
                Process.WaitForExit(1500);
            }
        }
        catch { /* 종료 단계의 예외는 무시 */ }
        Process?.Dispose();
        Process = null;
    }
}

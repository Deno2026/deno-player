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
            // mpv가 죽으면 stderr에 사유 찍힘 — 우리 log로 흡수해 진단
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8,
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
        Arg("--input-cursor=yes");          // mouse-pos observe용 (자체 매핑은 no)
        Arg("--cursor-autohide=no");        // 커서 숨김은 우리 OSD 타이머가 관리
        Arg("--audio-display=no");          // 오디오 재생 시 cover art 안 띄움
        // ⚠ --keep-open=yes 는 우리 child Win32 window(--wid) 환경에서 EOF 시 mpv가
        //   silent crash (exit -1)함. idle=yes로 mpv는 파일 끝나도 살아 있고,
        //   end-file(reason=eof) 이벤트는 정상 emit되어 OnEndFile이 next 트리거.
        Arg("--keep-open=no");
        // 우리 child hwnd 위에 다른 owned window(재생목록/최근)가 슬라이드 인할 때
        // mpv가 visible region 변경을 surface 재생성으로 응답하면서 영상이 잠깐 흔들림.
        // d3d11 백엔드를 명시하고 video sync를 audio에 묶어두면 surface 재생성 빈도가
        // 줄어 GPU compositor에 의한 partial-occlusion redraw에 안정적으로 반응한다.
        Arg("--vo=gpu");
        Arg("--gpu-api=d3d11");
        Arg("--video-sync=audio");
        Arg("--hwdec=auto-safe");
        // mpv 내부 OSD/창 드래그 핸들러 무력화 (우리 WPF가 chrome 관리)
        Arg("--no-window-dragging");
        Arg("--image-display-duration=inf");
        Arg("--hr-seek=yes");
        Arg("--cache=yes");
        Arg("--background=color");
        Arg("--background-color=#0B0E0C");
        Arg("--osd-font=Segoe UI");
        Arg($"--input-ipc-server={pipePath}");
        Arg($"--wid={videoHostHwnd.ToInt64()}");

        var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("mpv 프로세스를 시작하지 못했습니다.");
        proc.EnableRaisingEvents = true;

        // mpv가 stderr/stdout으로 토해내는 마지막 메시지는 crash 진단에 결정적
        proc.OutputDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) AppLog.Info("mpv> " + e.Data); };
        proc.ErrorDataReceived  += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) AppLog.Warn("mpv! " + e.Data); };
        try { proc.BeginOutputReadLine(); } catch { }
        try { proc.BeginErrorReadLine(); }  catch { }

        proc.Exited += (_, _) =>
        {
            AppLog.Warn($"mpv exited (pid={proc.Id}, exit={proc.ExitCode})");
            Crashed?.Invoke();
        };
        Process = proc;
        AppLog.Info($"mpv started pid={proc.Id} pipe={pipePath}");
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

using System.IO;
using System.Text;

namespace DenoVideoPlayer.Services;

/// <summary>
/// 가벼운 파일 로거. %APPDATA%\DenoVideoPlayer\log.txt 한 파일.
/// 사용자 문제 보고 시 실제로 무엇이 일어났는지 추적용.
/// 노이즈 줄이려고 Debug 레벨은 기본 비활성.
/// </summary>
public static class AppLog
{
    public enum Level { Debug, Info, Warn, Error }

    public static bool DebugEnabled { get; set; } = false;
    public static long MaxBytes { get; set; } = 1 * 1024 * 1024;   // 1 MB
    public static int  RotateKeep { get; set; } = 1;               // log.txt, log.txt.1

    private static readonly object Sync = new();
    private static readonly string Dir =
        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DenoVideoPlayer");
    private static readonly string FilePath = System.IO.Path.Combine(Dir, "log.txt");
    private static bool _started;

    public static string Path => FilePath;

    public static void Start(string sessionTag)
    {
        try { Directory.CreateDirectory(Dir); } catch { }
        _started = true;
        Info($"--- session start [{sessionTag}] pid={Environment.ProcessId} cwd={Environment.CurrentDirectory}");
    }

    public static void Debug(string msg) { if (DebugEnabled) Write(Level.Debug, msg); }
    public static void Info(string msg)  => Write(Level.Info, msg);
    public static void Warn(string msg)  => Write(Level.Warn, msg);
    public static void Error(string msg, Exception? ex = null)
    {
        Write(Level.Error, ex is null ? msg : $"{msg} :: {ex.GetType().Name}: {ex.Message}");
        if (ex is not null) Write(Level.Error, ex.StackTrace ?? "(no stack)");
    }

    private static void Write(Level lv, string msg)
    {
        if (!_started) return;
        try
        {
            lock (Sync)
            {
                RotateIfNeeded();
                var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {lv,-5} {msg}\n";
                File.AppendAllText(FilePath, line, new UTF8Encoding(false));
            }
        }
        catch
        {
            // 로깅 실패가 앱을 죽이면 안 됨
        }
    }

    private static void RotateIfNeeded()
    {
        try
        {
            if (!File.Exists(FilePath)) return;
            var fi = new FileInfo(FilePath);
            if (fi.Length < MaxBytes) return;

            // 가장 오래된 백업 정리 후 시프트
            for (var i = RotateKeep; i >= 1; i--)
            {
                var src = i == 1 ? FilePath : FilePath + "." + (i - 1);
                var dst = FilePath + "." + i;
                if (File.Exists(dst)) File.Delete(dst);
                if (File.Exists(src)) File.Move(src, dst);
            }
        }
        catch
        {
            // 회전 실패 시 그냥 진행
        }
    }
}

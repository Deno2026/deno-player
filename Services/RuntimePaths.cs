using System.IO;

namespace DenoVideoPlayer.Services;

public static class RuntimePaths
{
    private const string AppDirectoryName = "DenoVideoPlayer";

    public static string AppDataRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppDirectoryName);

    public static string RuntimeRoot => Path.Combine(AppDataRoot, "runtime");

    public static string MpvDirectory => Path.Combine(RuntimeRoot, "mpv");

    public static string MpvExe => Path.Combine(MpvDirectory, "mpv.exe");

    public static string FfmpegDirectory => Path.Combine(RuntimeRoot, "ffmpeg");

    public static string FfmpegExe => Path.Combine(FfmpegDirectory, "ffmpeg.exe");
}

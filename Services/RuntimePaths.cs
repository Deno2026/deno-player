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

    public static string FfprobeExe => Path.Combine(FfmpegDirectory, "ffprobe.exe");

    // Only the currently running app body is a trusted legacy promotion source.
    // Do not walk parent directories looking for runtime executables.
    public static string LegacyBundledMpvDirectory =>
        Path.Combine(AppContext.BaseDirectory, "runtime", "mpv");

    public static string LegacyBundledMpvExe =>
        Path.Combine(LegacyBundledMpvDirectory, "mpv.exe");

    public static string LegacyBundledFfmpegDirectory =>
        Path.Combine(AppContext.BaseDirectory, "runtime", "ffmpeg");
}

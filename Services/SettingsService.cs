using System.IO;
using System.Text.Json;
using DenoVideoPlayer.Models;

namespace DenoVideoPlayer.Services;

/// <summary>%APPDATA%\DenoVideoPlayer\settings.json. 저장 실패는 조용히 무시(앱 동작 우선).</summary>
public sealed class SettingsService
{
    private static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DenoVideoPlayer");
    private static readonly string FilePath = Path.Combine(Dir, "settings.json");
    private static readonly string LegacyDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DenoPlayer");
    private static readonly string LegacyFilePath = Path.Combine(LegacyDir, "settings.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public AppSettings Load()
    {
        try
        {
            MigrateLegacySettings();
            if (!File.Exists(FilePath)) return AppSettings.Defaults();
            using var fs = File.OpenRead(FilePath);
            var s = JsonSerializer.Deserialize<AppSettings>(fs, JsonOpts);
            return s ?? AppSettings.Defaults();
        }
        catch
        {
            return AppSettings.Defaults();
        }
    }

    public void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            var tmp = FilePath + ".tmp";
            using (var fs = File.Create(tmp))
                JsonSerializer.Serialize(fs, settings, JsonOpts);
            File.Move(tmp, FilePath, overwrite: true);
        }
        catch
        {
            // 설정 저장 실패는 사용자 워크플로를 방해하지 않는다.
        }
    }

    private static void MigrateLegacySettings()
    {
        try
        {
            if (File.Exists(FilePath) || !File.Exists(LegacyFilePath)) return;
            Directory.CreateDirectory(Dir);
            File.Copy(LegacyFilePath, FilePath, overwrite: false);
        }
        catch
        {
            // 이전 설정 복사 실패도 앱 시작을 막지 않는다.
        }
    }
}

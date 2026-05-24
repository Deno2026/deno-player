using System.IO;
using System.Text.Json;
using DenoPlayer.Models;

namespace DenoPlayer.Services;

/// <summary>%APPDATA%\DenoPlayer\settings.json. 저장 실패는 조용히 무시(앱 동작 우선).</summary>
public sealed class SettingsService
{
    private static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DenoPlayer");
    private static readonly string FilePath = Path.Combine(Dir, "settings.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public AppSettings Load()
    {
        try
        {
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
}

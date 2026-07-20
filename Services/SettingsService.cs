using System.IO;
using System.Text.Json;
using DenoVideoPlayer.Models;

namespace DenoVideoPlayer.Services;

/// <summary>%APPDATA%\DenoVideoPlayer\settings.json. 손상 파일은 보존하고 기본값으로 복구한다.</summary>
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
            return (s ?? AppSettings.Defaults()).Normalize();
        }
        catch (Exception ex)
        {
            AppLog.Error("Settings load failed; using defaults.", ex);
            PreserveCorruptSettings();
            return AppSettings.Defaults();
        }
    }

    public void Save(AppSettings settings)
    {
        var tmp = FilePath + ".tmp";
        try
        {
            settings.Normalize();
            Directory.CreateDirectory(Dir);
            using (var fs = File.Create(tmp))
            {
                JsonSerializer.Serialize(fs, settings, JsonOpts);
                fs.Flush(flushToDisk: true);
            }
            if (File.Exists(FilePath))
                File.Replace(tmp, FilePath, destinationBackupFileName: null, ignoreMetadataErrors: true);
            else
                File.Move(tmp, FilePath);
        }
        catch (Exception ex)
        {
            // 설정 저장 실패는 사용자 워크플로를 방해하지 않는다.
            AppLog.Error("Settings save failed.", ex);
        }
        finally
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
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

    private static void PreserveCorruptSettings()
    {
        try
        {
            if (!File.Exists(FilePath)) return;
            var stamp = File.GetLastWriteTimeUtc(FilePath).ToString("yyyyMMddTHHmmssfffZ");
            var backup = Path.Combine(Dir, $"settings.corrupt-{stamp}.json");
            if (!File.Exists(backup)) File.Copy(FilePath, backup, overwrite: false);
            AppLog.Warn($"Corrupt settings preserved: {backup}");
        }
        catch (Exception ex)
        {
            AppLog.Warn($"Could not preserve corrupt settings: {ex.Message}");
        }
    }
}

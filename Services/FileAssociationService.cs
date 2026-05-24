using System.Diagnostics;
using Microsoft.Win32;

namespace DenoPlayer.Services;

/// <summary>
/// HKCU 기반(관리자 권한 불필요) 파일 연결 등록.
/// install.ps1과 동일한 키를 코드에서도 갱신할 수 있게 분리.
/// Windows 10/11은 default 앱 변경 자체는 UserChoice hash로 보호하므로,
/// 우리는 "연결 프로그램 후보"로 등록만 하고 default 지정은 시스템 설정으로 안내한다.
/// </summary>
public static class FileAssociationService
{
    public const string AppKey = @"Software\Classes\Applications\DenoPlayer.exe";
    public const string ProgId = "Applications\\DenoPlayer.exe";

    public static void RegisterApplication(string exePath, string friendlyName = "Deno Player")
    {
        using var hkcu = Registry.CurrentUser;
        using var appKey = hkcu.CreateSubKey(AppKey, writable: true);
        appKey.SetValue("FriendlyAppName", friendlyName, RegistryValueKind.String);
        using (var cmd = hkcu.CreateSubKey($@"{AppKey}\shell\open\command", writable: true))
            cmd.SetValue("", $"\"{exePath}\" \"%1\"", RegistryValueKind.String);
    }

    /// <summary>
    /// SupportedTypes(앱이 어떤 확장자 지원 명시) + 각 확장자의 OpenWithProgids 추가.
    /// extensions에 포함 안 된 확장자는 우리 ProgID를 제거(toggle 해제).
    /// </summary>
    public static void SyncExtensions(IEnumerable<string> selected, IEnumerable<string> allKnown)
    {
        using var hkcu = Registry.CurrentUser;
        var selectedSet = new HashSet<string>(selected, StringComparer.OrdinalIgnoreCase);
        var all = allKnown.ToList();

        // SupportedTypes: 선택된 것만
        using (var supported = hkcu.CreateSubKey($@"{AppKey}\SupportedTypes", writable: true))
        {
            // 기존 정리
            foreach (var v in supported.GetValueNames()) supported.DeleteValue(v, false);
            foreach (var e in selectedSet) supported.SetValue(e, "", RegistryValueKind.String);
        }

        // 각 확장자 OpenWithProgids — 선택된 건 추가, 비선택은 제거
        foreach (var e in all)
        {
            using var owp = hkcu.CreateSubKey($@"Software\Classes\{e}\OpenWithProgids", writable: true);
            if (owp is null) continue;
            if (selectedSet.Contains(e))
            {
                owp.SetValue(ProgId, Array.Empty<byte>(), RegistryValueKind.None);
            }
            else
            {
                try { owp.DeleteValue(ProgId); } catch { /* 없으면 무시 */ }
            }
        }
    }

    /// <summary>Windows 10/11 기본 앱 설정 화면을 연다 (사용자가 직접 default 지정).</summary>
    public static void OpenDefaultAppsSettings()
    {
        try
        {
            Process.Start(new ProcessStartInfo("ms-settings:defaultapps") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppLog.Error("OpenDefaultAppsSettings failed", ex);
        }
    }
}

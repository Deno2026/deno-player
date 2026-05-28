using System.Diagnostics;
using Microsoft.Win32;

namespace DenoVideoPlayer.Services;

/// <summary>
/// HKCU 기반 파일 연결 등록 (관리자 권한 불필요).
///
/// Windows 10/11에서 "기본 앱"으로 지정되려면 단순히 Applications\&lt;exe&gt; 등록만으로는
/// 부족하다 (그건 'Open With' 후보 용도). 다음 3종 세트가 필요:
///
///   1) ProgID                ─ HKCU\Software\Classes\DenoVideoPlayer.Media
///                              shell\open\command, DefaultIcon
///   2) Application Key       ─ HKCU\Software\Classes\Applications\DenoVideoPlayer.exe
///                              FriendlyAppName + SupportedTypes (Open With 후보)
///   3) Capabilities          ─ HKCU\Software\DenoVideoPlayer\Capabilities
///                              ApplicationName / Description / FileAssociations
///      + RegisteredApplications "Deno Video Player" = "Software\DenoVideoPlayer\Capabilities"
///
/// 위 셋이 다 있어야 Windows 설정 → 기본 앱 화면에 "Deno Video Player" 항목이 보이고,
/// 사용자가 선택했을 때 UserChoice 해시 검증을 통과한다.
/// </summary>
public static class FileAssociationService
{
    public const string AppName        = "Deno Video Player";
    private const string LegacyAppName = "Deno Player";
    public const string AppKey         = @"Software\Classes\Applications\DenoVideoPlayer.exe";
    public const string ProgId         = "DenoVideoPlayer.Media";          // 실제 ProgID
    public const string OpenWithProgId = "Applications\\DenoVideoPlayer.exe"; // Open With 호환용
    public const string CapabilitiesKey = @"Software\DenoVideoPlayer\Capabilities";
    private const string LegacyAppKey = @"Software\Classes\Applications\DenoPlayer.exe";
    private const string LegacyProgId = "DenoPlayer.Media";
    private const string LegacyOpenWithProgId = "Applications\\DenoPlayer.exe";
    private const string LegacyCapabilitiesKey = @"Software\DenoPlayer\Capabilities";

    public static void RegisterApplication(string exePath, string friendlyName = AppName)
    {
        using var hkcu = Registry.CurrentUser;
        RemoveLegacyRegistrations(hkcu);

        // 1) Application key — "Open With" 메뉴용 (기존 호환)
        using (var appKey = hkcu.CreateSubKey(AppKey, writable: true))
        {
            appKey!.SetValue("FriendlyAppName", friendlyName, RegistryValueKind.String);
        }
        using (var cmd = hkcu.CreateSubKey($@"{AppKey}\shell\open\command", writable: true))
        {
            cmd!.SetValue("", $"\"{exePath}\" \"%1\"", RegistryValueKind.String);
        }

        // 2) ProgID — 실제 default 앱 지정용
        using (var prog = hkcu.CreateSubKey($@"Software\Classes\{ProgId}", writable: true))
        {
            prog!.SetValue("", "Deno Video Player Media File", RegistryValueKind.String);
            prog.SetValue("FriendlyTypeName", "Deno Video Player 미디어", RegistryValueKind.String);
        }
        using (var icon = hkcu.CreateSubKey($@"Software\Classes\{ProgId}\DefaultIcon", writable: true))
        {
            icon!.SetValue("", $"\"{exePath}\",0", RegistryValueKind.String);
        }
        using (var cmd = hkcu.CreateSubKey($@"Software\Classes\{ProgId}\shell\open\command", writable: true))
        {
            cmd!.SetValue("", $"\"{exePath}\" \"%1\"", RegistryValueKind.String);
        }

        // 3) Capabilities — Windows 10/11 "기본 앱" UI 등록
        using (var caps = hkcu.CreateSubKey(CapabilitiesKey, writable: true))
        {
            caps!.SetValue("ApplicationName", friendlyName, RegistryValueKind.String);
            caps.SetValue("ApplicationDescription",
                "로컬 미디어를 빠르게 여는 가벼운 mpv 셸 플레이어",
                RegistryValueKind.String);
            caps.SetValue("ApplicationIcon", $"\"{exePath}\",0", RegistryValueKind.String);
        }

        // 4) RegisteredApplications — Capabilities를 시스템에 노출
        using (var regApps = hkcu.CreateSubKey(@"Software\RegisteredApplications", writable: true))
        {
            regApps!.DeleteValue(LegacyAppName, throwOnMissingValue: false);
            regApps.SetValue(AppName, CapabilitiesKey, RegistryValueKind.String);
        }
    }

    /// <summary>
    /// 선택된 확장자에 대해 ProgID 매핑 + OpenWithProgids 등록.
    /// 비선택된 확장자는 우리 ProgID/AppKey 값을 제거.
    /// </summary>
    public static void SyncExtensions(IEnumerable<string> selected, IEnumerable<string> allKnown)
    {
        using var hkcu = Registry.CurrentUser;
        var selectedSet = new HashSet<string>(selected, StringComparer.OrdinalIgnoreCase);
        var all = allKnown.ToList();

        // SupportedTypes: 선택된 확장자만 (Open With 후보 노출용)
        using (var supported = hkcu.CreateSubKey($@"{AppKey}\SupportedTypes", writable: true))
        {
            foreach (var v in supported!.GetValueNames()) supported.DeleteValue(v, throwOnMissingValue: false);
            foreach (var e in selectedSet) supported.SetValue(e, "", RegistryValueKind.String);
        }

        // Capabilities\FileAssociations: 선택된 확장자 → ProgID 매핑
        // (이 키가 있어야 Windows '기본 앱'이 우리를 그 확장자의 후보로 인식)
        using (var fa = hkcu.CreateSubKey($@"{CapabilitiesKey}\FileAssociations", writable: true))
        {
            foreach (var v in fa!.GetValueNames()) fa.DeleteValue(v, throwOnMissingValue: false);
            foreach (var e in selectedSet) fa.SetValue(e, ProgId, RegistryValueKind.String);
        }

        // 각 확장자 OpenWithProgids — 선택된 건 ProgID + 호환 키 둘 다 추가, 비선택은 제거
        foreach (var e in all)
        {
            using var owp = hkcu.CreateSubKey($@"Software\Classes\{e}\OpenWithProgids", writable: true);
            if (owp is null) continue;
            try { owp.DeleteValue(LegacyProgId); } catch { }
            try { owp.DeleteValue(LegacyOpenWithProgId); } catch { }
            if (selectedSet.Contains(e))
            {
                owp.SetValue(ProgId, Array.Empty<byte>(), RegistryValueKind.None);
                owp.SetValue(OpenWithProgId, Array.Empty<byte>(), RegistryValueKind.None);
            }
            else
            {
                try { owp.DeleteValue(ProgId); } catch { }
                try { owp.DeleteValue(OpenWithProgId); } catch { }
            }
        }
    }

    private static void RemoveLegacyRegistrations(RegistryKey hkcu)
    {
        try { hkcu.DeleteSubKeyTree(LegacyAppKey, throwOnMissingSubKey: false); } catch { }
        try { hkcu.DeleteSubKeyTree($@"Software\Classes\{LegacyProgId}", throwOnMissingSubKey: false); } catch { }
        try { hkcu.DeleteSubKeyTree(LegacyCapabilitiesKey, throwOnMissingSubKey: false); } catch { }

        try
        {
            using var regApps = hkcu.OpenSubKey(@"Software\RegisteredApplications", writable: true);
            regApps?.DeleteValue(LegacyAppName, throwOnMissingValue: false);
        }
        catch { }
    }

    /// <summary>Windows 10/11 "기본 앱" 화면을 Deno Video Player 페이지로 바로 연다.</summary>
    public static void OpenDefaultAppsSettings()
    {
        try
        {
            // Windows 11: 우리 앱 페이지로 직접
            var uri = $"ms-settings:defaultapps?registeredAppUser={Uri.EscapeDataString(AppName)}";
            Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppLog.Error("OpenDefaultAppsSettings failed", ex);
            try { Process.Start(new ProcessStartInfo("ms-settings:defaultapps") { UseShellExecute = true }); }
            catch { }
        }
    }
}

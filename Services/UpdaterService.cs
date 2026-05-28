using Velopack;
using Velopack.Sources;

namespace DenoVideoPlayer.Services;

/// <summary>
/// Velopack 기반 자동 업데이트. App startup 시점에 VelopackApp.Build().Run() — 첫 줄 필수.
/// 별도 thread에서 update channel(GitHub release) 확인 + background download + 다음 launch 시
/// 자동 적용. 사용자가 수동 install 안 해도 다음 실행 때 새 버전 가동.
///
/// Update channel: 우선 환경변수 DENO_PLAYER_UPDATE_URL, 없으면 public GitHub repo.
/// </summary>
public static class UpdaterService
{
    /// <summary>
    /// Default channel. 사용자가 직접 release zip을 host하는 다른 URL로 바꾸려면
    /// 환경변수 DENO_PLAYER_UPDATE_URL 설정.
    /// </summary>
    public const string DefaultChannelUrl = "https://github.com/Deno2026/deno-player";

    /// <summary>
    /// 백그라운드 update check만 (download/install 안 함). 새 버전 발견하면 새 version
    /// string 반환 — UI가 버튼 활성화. download/install은 사용자 click 후 ApplyAsync.
    /// 강제 강제 안 함 = opt-in pull 패턴.
    /// </summary>
    public static async Task<(bool available, string? newVersion, UpdateInfo? info)> CheckAsync()
    {
        try
        {
            var url = Environment.GetEnvironmentVariable("DENO_PLAYER_UPDATE_URL")
                      ?? DefaultChannelUrl;
            AppLog.Info($"Updater: checking {url}");
            var source = new GithubSource(url, null, prerelease: false);
            var mgr = new UpdateManager(source);
            var info = await mgr.CheckForUpdatesAsync().ConfigureAwait(false);
            if (info is null) { AppLog.Info("Updater: no updates"); return (false, null, null); }
            var ver = info.TargetFullRelease.Version.ToString();
            AppLog.Info($"Updater: new version {ver} available (opt-in)");
            return (true, ver, info);
        }
        catch (Exception ex)
        {
            AppLog.Warn($"Updater: check failed: {ex.Message}");
            return (false, null, null);
        }
    }

    /// <summary>
    /// 사용자가 UI 버튼 click 시 호출 — download + apply + 재시작.
    /// Velopack install된 경우 자동 install + restart. portable 실행이면 browser로 release page 열기.
    /// </summary>
    public static async Task<bool> ApplyAsync(UpdateInfo info)
    {
        try
        {
            var url = Environment.GetEnvironmentVariable("DENO_PLAYER_UPDATE_URL")
                      ?? DefaultChannelUrl;
            var source = new GithubSource(url, null, prerelease: false);
            var mgr = new UpdateManager(source);

            if (!mgr.IsInstalled)
            {
                // portable / dev build — 자동 install 못함. release page 열기 (사용자가 수동 다운).
                AppLog.Info("Updater: portable mode — opening release page in browser");
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = $"{url}/releases/latest",
                    UseShellExecute = true
                });
                return true;
            }

            AppLog.Info($"Updater: downloading {info.TargetFullRelease.Version}...");
            await mgr.DownloadUpdatesAsync(info).ConfigureAwait(false);
            AppLog.Info("Updater: applying + restarting");
            mgr.ApplyUpdatesAndRestart(info);
            return true;
        }
        catch (Exception ex)
        {
            AppLog.Error("Updater.ApplyAsync", ex);
            return false;
        }
    }
}

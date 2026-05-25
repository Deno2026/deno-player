using Velopack;
using Velopack.Sources;

namespace DenoPlayer.Services;

/// <summary>
/// Velopack 기반 자동 업데이트. App startup 시점에 VelopackApp.Build().Run() — 첫 줄 필수.
/// 별도 thread에서 update channel(GitHub release) 확인 + background download + 다음 launch 시
/// 자동 적용. 사용자가 수동 install 안 해도 다음 실행 때 새 버전 가동.
///
/// Update channel: 우선 환경변수 DENO_PLAYER_UPDATE_URL, 없으면 default GitHub repo.
/// </summary>
public static class UpdaterService
{
    /// <summary>
    /// Default channel — 별도 public repo에 release를 publish하는 패턴(코드 repo는 private 유지).
    /// 사용자가 직접 release zip을 host하는 다른 URL로 바꾸려면 환경변수 DENO_PLAYER_UPDATE_URL 설정.
    /// </summary>
    public const string DefaultChannelUrl = "https://github.com/Deno2026/deno-player-releases";

    /// <summary>Velopack 진입점. App.OnAppStartup 첫 줄에서 호출.</summary>
    public static void RunVelopack(string[] args)
    {
        try
        {
            // Velopack 0.0.1298 signature: Run() takes no args (process args 자체 사용).
            VelopackApp.Build().Run();
        }
        catch (Exception ex)
        {
            AppLog.Warn($"VelopackApp.Run failed: {ex.Message}");
        }
    }

    /// <summary>
    /// 백그라운드로 update check + download. UI thread block 안 함. 새 버전 받으면 다음
    /// launch 시 자동 적용 (Velopack가 처리).
    /// </summary>
    public static async Task CheckAndStageAsync()
    {
        try
        {
            var url = Environment.GetEnvironmentVariable("DENO_PLAYER_UPDATE_URL")
                      ?? DefaultChannelUrl;
            AppLog.Info($"Updater: checking {url}");
            var source = new GithubSource(url, null, prerelease: false);
            var mgr = new UpdateManager(source);

            if (!mgr.IsInstalled)
            {
                // 개발 빌드 / portable 실행. Velopack 설치 안 됨. skip.
                AppLog.Info("Updater: not installed via Velopack — skipping (likely dev/portable build)");
                return;
            }

            var info = await mgr.CheckForUpdatesAsync().ConfigureAwait(false);
            if (info is null)
            {
                AppLog.Info("Updater: no updates available");
                return;
            }

            AppLog.Info($"Updater: new version {info.TargetFullRelease.Version} available — downloading");
            await mgr.DownloadUpdatesAsync(info).ConfigureAwait(false);
            AppLog.Info("Updater: downloaded. Will apply on next launch.");
            // 즉시 재시작은 사용자 흐름 방해 — 다음 launch에 자동 적용. 사용자가 원하면
            // 다음 라운드에 "지금 재시작" UI를 추가할 수 있음 (예: mgr.ApplyUpdatesAndRestart(info)).
        }
        catch (Exception ex)
        {
            // 네트워크 단절, 잘못된 URL, 권한 문제 등 — 앱 동작에 영향 안 줌.
            AppLog.Warn($"Updater: check failed: {ex.Message}");
        }
    }
}

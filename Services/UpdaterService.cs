using System.IO;
using Velopack;
using Velopack.Sources;

namespace DenoVideoPlayer.Services;

public sealed record UpdateCheckResult(
    bool Available,
    string? NewVersion,
    UpdateInfo? Info,
    bool ReadyToApply,
    bool Portable);

/// <summary>
/// GitHub Releases + Velopack 기반 업데이트.
/// 설치판은 백그라운드에서 새 버전을 내려받아 준비하고, 사용자가 버튼을 누르면 재시작 적용한다.
/// Portable/dev 실행은 자동 적용이 불가능하므로 최신 릴리스 페이지를 연다.
/// </summary>
public static class UpdaterService
{
    public const string DefaultChannelUrl = "https://github.com/Deno2026/deno-video-player";

    public static async Task<(bool available, string? newVersion, UpdateInfo? info)> CheckAsync()
    {
        var result = await CheckAndPrepareAsync(autoDownload: false).ConfigureAwait(false);
        return (result.Available, result.NewVersion, result.Info);
    }

    public static async Task<UpdateCheckResult> CheckAndPrepareAsync(
        CancellationToken ct = default,
        bool autoDownload = true)
    {
        try
        {
            var (url, mgr) = CreateManager();
            if (!mgr.IsInstalled)
            {
                AppLog.Info("Updater: portable/dev mode - automatic update check skipped");
                return new UpdateCheckResult(false, null, null, false, true);
            }

            var pending = mgr.UpdatePendingRestart;
            if (pending is not null)
            {
                AppLog.Info($"Updater: prepared update {pending.Version} pending restart");
                return new UpdateCheckResult(true, pending.Version.ToString(), null, true, false);
            }

            AppLog.Info($"Updater: checking {url}");
            var info = await mgr.CheckForUpdatesAsync().ConfigureAwait(false);
            if (ct.IsCancellationRequested) throw new OperationCanceledException(ct);
            if (info is null)
            {
                AppLog.Info("Updater: no updates");
                return new UpdateCheckResult(false, null, null, false, false);
            }

            var ver = info.TargetFullRelease.Version.ToString();
            if (!autoDownload)
            {
                AppLog.Info($"Updater: new version {ver} available");
                return new UpdateCheckResult(true, ver, info, false, false);
            }

            try
            {
                AppLog.Info($"Updater: background download {ver}...");
                var lastProgress = -1;
                await mgr.DownloadUpdatesAsync(info, progress =>
                {
                    if (progress / 10 == lastProgress / 10) return;
                    lastProgress = progress;
                    AppLog.Info($"Updater: download {progress}%");
                }, ct).ConfigureAwait(false);

                pending = mgr.UpdatePendingRestart ?? info.TargetFullRelease;
                AppLog.Info($"Updater: update {pending.Version} ready to apply");
                return new UpdateCheckResult(true, pending.Version.ToString(), info, true, false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                AppLog.Warn($"Updater: background download failed: {ex.Message}");
                return new UpdateCheckResult(true, ver, info, false, false);
            }
        }
        catch (OperationCanceledException)
        {
            AppLog.Info("Updater: cancelled");
            return new UpdateCheckResult(false, null, null, false, false);
        }
        catch (Exception ex)
        {
            AppLog.Warn($"Updater: check failed: {ex.Message}");
            return new UpdateCheckResult(false, null, null, false, false);
        }
    }

    public static async Task<bool> ApplyAsync(UpdateInfo? info)
    {
        try
        {
            var (url, mgr) = CreateManager();
            if (!mgr.IsInstalled)
            {
                OpenLatestRelease(url);
                return true;
            }

            var pending = mgr.UpdatePendingRestart;
            if (pending is not null)
            {
                AppLog.Info($"Updater: applying prepared update {pending.Version} + restarting");
                RuntimeDependencyService.PreserveExistingRuntimeCache();
                mgr.ApplyUpdatesAndRestart(pending);
                return true;
            }

            info ??= await mgr.CheckForUpdatesAsync().ConfigureAwait(false);
            if (info is null)
            {
                AppLog.Info("Updater: no update to apply");
                return false;
            }

            AppLog.Info($"Updater: downloading {info.TargetFullRelease.Version}...");
            var lastProgress = -1;
            await mgr.DownloadUpdatesAsync(info, progress =>
            {
                if (progress / 10 == lastProgress / 10) return;
                lastProgress = progress;
                AppLog.Info($"Updater: manual download {progress}%");
            }, CancellationToken.None)
                .ConfigureAwait(false);

            pending = mgr.UpdatePendingRestart ?? info.TargetFullRelease;
            AppLog.Info($"Updater: applying {pending.Version} + restarting");
            RuntimeDependencyService.PreserveExistingRuntimeCache();
            mgr.ApplyUpdatesAndRestart(pending);
            return true;
        }
        catch (Exception ex)
        {
            AppLog.Error("Updater.ApplyAsync", ex);
            return false;
        }
    }

    private static (string Url, UpdateManager Manager) CreateManager()
    {
        var url = Environment.GetEnvironmentVariable("DENO_PLAYER_UPDATE_URL")
                  ?? DefaultChannelUrl;
        var source = CreateUpdateSource(url);
        return (url, new UpdateManager(source));
    }

    private static IUpdateSource CreateUpdateSource(string url)
    {
        if (TryCreateLocalFileSource(url, out var source))
        {
            return source;
        }

        return new GithubSource(url, null, prerelease: false);
    }

    private static bool TryCreateLocalFileSource(string url, out IUpdateSource source)
    {
        source = null!;
        var trimmed = Environment.ExpandEnvironmentVariables(url.Trim().Trim('"'));
        if (Directory.Exists(trimmed))
        {
            AppLog.Info($"Updater: using local feed {trimmed}");
            source = new SimpleFileSource(new DirectoryInfo(trimmed));
            return true;
        }

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            && uri.IsFile
            && Directory.Exists(uri.LocalPath))
        {
            AppLog.Info($"Updater: using local feed {uri.LocalPath}");
            source = new SimpleFileSource(new DirectoryInfo(uri.LocalPath));
            return true;
        }

        return false;
    }

    private static void OpenLatestRelease(string url)
    {
        AppLog.Info("Updater: portable mode - opening release page in browser");
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = $"{url}/releases/latest",
            UseShellExecute = true
        });
    }
}

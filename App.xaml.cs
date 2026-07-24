using System.Windows;
using System.Windows.Threading;
using DenoVideoPlayer.Services;

namespace DenoVideoPlayer;

public partial class App : Application
{
    public static string[] StartupArgs { get; private set; } = Array.Empty<string>();
    private SingleInstance? _single;
    private MainWindow? _win;     // cross-thread 안전한 직접 캐시 — Application.MainWindow는 UI affinity 가질 수 있음
    private CancellationTokenSource? _updateCts;

    private void OnAppStartup(object sender, StartupEventArgs e)
    {
        StartupArgs = e.Args ?? Array.Empty<string>();
        AppLog.Start("startup");
        if (StartupArgs.Length > 0) AppLog.Info($"args: {string.Join(" | ", StartupArgs)}");

        var startupSettings = new SettingsService().Load();
        LocalizationService.Apply(startupSettings.Language);

        // ─ Single instance ─ 이미 떠 있으면 첫 인스턴스에 인자 보내고 종료
        _single = new SingleInstance();
        if (!_single.TryClaim(StartupArgs))
        {
            AppLog.Info("another instance owns the mutex — handing off and exiting");
            Shutdown(0);
            return;
        }
        AppLog.Info("single-instance owner");

        AppDomain.CurrentDomain.UnhandledException += (_, ex) =>
        {
            var msg = ex.ExceptionObject?.ToString() ?? "unknown";
            AppLog.Error("UnhandledException", ex.ExceptionObject as Exception);
            MessageBox.Show(LocalizationService.F("UnexpectedError", msg), "Deno Video Player",
                MessageBoxButton.OK, MessageBoxImage.Error);
        };
        DispatcherUnhandledException += (_, args) =>
        {
            AppLog.Error("DispatcherUnhandledException", args.Exception);
            MessageBox.Show(LocalizationService.F("UiError", args.Exception.Message), "Deno Video Player",
                MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        // 시작 설정은 언어 적용과 MainViewModel이 함께 사용한다. 같은 JSON을 첫 화면 전에
        // 두 번 동기 읽지 않도록 한 번 읽은 인스턴스를 그대로 넘긴다.
        _win = new MainWindow(startupSettings);
        MainWindow = _win;
        _win.Show();

        _updateCts = new CancellationTokenSource();
        StartUpdateLoop(_win.DataContext as ViewModels.MainViewModel, _updateCts.Token);

        _single.ArgsReceived += args =>
        {
            try
            {
                AppLog.Info($"second-instance args received: {string.Join(" | ", args)}");
                AppLog.Info($"  route: _win is {(_win is null ? "NULL" : "ok")}");
                _win?.ReceiveExternalArgs(args);
            }
            catch (Exception ex)
            {
                AppLog.Error("ArgsReceived handler crashed", ex);
            }
        };
    }

    private void OnAppExit(object sender, ExitEventArgs e)
    {
        AppLog.Info("--- session end");
        try { _updateCts?.Cancel(); } catch { }
        _updateCts?.Dispose();
        _updateCts = null;
        _single?.Dispose();
        _single = null;
    }

    private static void StartUpdateLoop(ViewModels.MainViewModel? vm, CancellationToken ct)
    {
        if (vm is null) return;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(3), ct).ConfigureAwait(false);
                while (!ct.IsCancellationRequested)
                {
                    // 확인 전에는 update package를 내려받지 않는다. 사용자가 prompt에서
                    // 동의한 뒤 ApplyAsync가 다운로드와 적용을 진행한다.
                    var result = await UpdaterService.CheckAndPrepareAsync(
                        ct,
                        autoDownload: false).ConfigureAwait(false);
                    if (result.Available && result.NewVersion is not null)
                    {
                        vm.SetPendingUpdate(
                            result.NewVersion,
                            result.Info,
                            result.ReadyToApply,
                            result.Portable);
                    }

                    await Task.Delay(TimeSpan.FromHours(6), ct).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // normal shutdown
            }
            catch (Exception ex)
            {
                AppLog.Warn($"Updater loop stopped: {ex.Message}");
            }
        }, ct);
    }
}

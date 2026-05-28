using System.Windows;
using System.Windows.Threading;
using DenoPlayer.Services;

namespace DenoPlayer;

public partial class App : Application
{
    public static string[] StartupArgs { get; private set; } = Array.Empty<string>();
    private SingleInstance? _single;
    private MainWindow? _win;     // cross-thread 안전한 직접 캐시 — Application.MainWindow는 UI affinity 가질 수 있음

    private void OnAppStartup(object sender, StartupEventArgs e)
    {
        // Velopack 진입점 — 첫 줄 필수. update 적용 흐름 / restart 후 routing 처리.
        UpdaterService.RunVelopack(e.Args ?? Array.Empty<string>());

        StartupArgs = e.Args ?? Array.Empty<string>();
        AppLog.Start("startup");
        if (StartupArgs.Length > 0) AppLog.Info($"args: {string.Join(" | ", StartupArgs)}");

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
            MessageBox.Show("예기치 못한 오류:\n" + msg, "Deno Video Player",
                MessageBoxButton.OK, MessageBoxImage.Error);
        };
        DispatcherUnhandledException += (_, args) =>
        {
            AppLog.Error("DispatcherUnhandledException", args.Exception);
            MessageBox.Show("UI 예외:\n" + args.Exception.Message, "Deno Video Player",
                MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        _win = new MainWindow();
        MainWindow = _win;
        _win.Show();

        // 백그라운드 update check (강제 X, opt-in). 새 버전 발견하면 ViewModel.IsUpdateAvailable=true
        // → TopBar update button 활성. 사용자가 click 시 download + 적용 + 재시작.
        _ = Task.Run(async () =>
        {
            var (available, ver, info) = await UpdaterService.CheckAsync();
            if (available && ver is not null && info is not null && _win?.DataContext is ViewModels.MainViewModel vm)
                vm.SetPendingUpdate(ver, info);
        });

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
        _single?.Dispose();
        _single = null;
    }
}

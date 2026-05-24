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
            MessageBox.Show("예기치 못한 오류:\n" + msg, "Deno Player",
                MessageBoxButton.OK, MessageBoxImage.Error);
        };
        DispatcherUnhandledException += (_, args) =>
        {
            AppLog.Error("DispatcherUnhandledException", args.Exception);
            MessageBox.Show("UI 예외:\n" + args.Exception.Message, "Deno Player",
                MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        _win = new MainWindow();
        MainWindow = _win;
        _win.Show();

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

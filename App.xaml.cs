using System.Windows;
using System.Windows.Threading;
using DenoPlayer.Services;

namespace DenoPlayer;

public partial class App : Application
{
    public static string[] StartupArgs { get; private set; } = Array.Empty<string>();
    private SingleInstance? _single;

    private void OnAppStartup(object sender, StartupEventArgs e)
    {
        StartupArgs = e.Args ?? Array.Empty<string>();

        // ─ Single instance ─ 이미 떠 있으면 첫 인스턴스에 인자 보내고 종료
        _single = new SingleInstance();
        if (!_single.TryClaim(StartupArgs))
        {
            Shutdown(0);
            return;
        }

        AppDomain.CurrentDomain.UnhandledException += (_, ex) =>
        {
            var msg = ex.ExceptionObject?.ToString() ?? "unknown";
            MessageBox.Show("예기치 못한 오류:\n" + msg, "Deno Player",
                MessageBoxButton.OK, MessageBoxImage.Error);
        };
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show("UI 예외:\n" + args.Exception.Message, "Deno Player",
                MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        var win = new MainWindow();
        MainWindow = win;
        win.Show();

        _single.ArgsReceived += args =>
        {
            // 다른 인스턴스가 보낸 인자 → 메인 윈도우 활성화 + 파일 열기
            (MainWindow as MainWindow)?.ReceiveExternalArgs(args);
        };
    }

    private void OnAppExit(object sender, ExitEventArgs e)
    {
        _single?.Dispose();
        _single = null;
    }
}

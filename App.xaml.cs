using System.Windows;
using System.Windows.Threading;

namespace DenoPlayer;

public partial class App : Application
{
    public static string[] StartupArgs { get; private set; } = Array.Empty<string>();

    private void OnAppStartup(object sender, StartupEventArgs e)
    {
        StartupArgs = e.Args ?? Array.Empty<string>();

        // 미처리 예외는 사용자에게 짧게 알려주고 앱 보존
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

        if (StartupArgs.Length > 0)
        {
            // 첫 번째 인자가 파일이면 그 파일 열기
            var first = StartupArgs[0];
            if (System.IO.File.Exists(first))
                win.OpenFromExternal(first);
        }
    }

    private void OnAppExit(object sender, ExitEventArgs e)
    {
        // 명시적 정리는 MainWindow.Closing에서 처리; 이중 안전.
    }
}

using System.Windows;
using Velopack;

namespace DenoVideoPlayer;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        try
        {
            VelopackApp.Build().Run();
        }
        catch (Exception ex)
        {
            // portable/dev 실행은 계속 허용하되 설치·업데이트 hook 실패는 진단 가능하게 남긴다.
            System.Diagnostics.Trace.TraceError($"Velopack bootstrap failed: {ex}");
        }

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}

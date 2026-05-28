using System.Windows;
using Velopack;

namespace DenoVideoPlayer;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        try { VelopackApp.Build().Run(); } catch { }

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}

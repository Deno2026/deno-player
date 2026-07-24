using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace DenoVideoPlayer.Views;

public partial class HelpWindow : Window
{
    public HelpWindow()
    {
        InitializeComponent();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ClampToOwnerWorkArea(updatePosition: false);
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        ClampToOwnerWorkArea(updatePosition: true);
    }

    public void ScrollToTroubleshooting()
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            TroubleshootingSection.BringIntoView();
            GuideScroll.ScrollToHorizontalOffset(0);
        }), DispatcherPriority.Loaded);
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        Close();
        e.Handled = true;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void ClampToOwnerWorkArea(bool updatePosition)
    {
        var workArea = Owner is global::DenoVideoPlayer.MainWindow main
            ? main.GetCurrentMonitorWorkAreaBounds()
            : SystemParameters.WorkArea;
        const double margin = 16;
        var availableWidth = Math.Max(240, workArea.Width - margin * 2);
        var availableHeight = Math.Max(240, workArea.Height - margin * 2);

        MinWidth = Math.Min(MinWidth, availableWidth);
        MinHeight = Math.Min(MinHeight, availableHeight);
        MaxWidth = availableWidth;
        MaxHeight = availableHeight;
        Width = Math.Min(Width, availableWidth);
        Height = Math.Min(Height, availableHeight);

        if (!updatePosition) return;

        var minLeft = workArea.Left + margin;
        var minTop = workArea.Top + margin;
        var maxLeft = workArea.Right - Width - margin;
        var maxTop = workArea.Bottom - Height - margin;
        var currentLeft = double.IsFinite(Left)
            ? Left
            : workArea.Left + (workArea.Width - Width) / 2;
        var currentTop = double.IsFinite(Top)
            ? Top
            : workArea.Top + (workArea.Height - Height) / 2;
        Left = maxLeft < minLeft ? workArea.Left : Math.Clamp(currentLeft, minLeft, maxLeft);
        Top = maxTop < minTop ? workArea.Top : Math.Clamp(currentTop, minTop, maxTop);
    }
}

using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace DenoVideoPlayer.Views;

/// <summary>
/// owned child window로 띄우는 toast. WPF Border로 toast를 영상 위에 띄우면 WPF airspace
/// 한계로 HwndHost(mpv) 픽셀에 가려진다. 별도 hwnd라 z-order 위로.
/// PlaylistWindow처럼 owner에 묶여 다른 앱 위로 새지 않음.
/// </summary>
public partial class ToastWindow : Window
{
    private readonly DispatcherTimer _hideTimer;
    private const int VisibleMs = 2200;
    private const int FadeOutMs = 250;
    private const int FadeInMs  = 150;

    public ToastWindow()
    {
        InitializeComponent();
        _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(VisibleMs) };
        _hideTimer.Tick += (_, _) => { _hideTimer.Stop(); FadeOut(); };
    }

    public void Show(string message)
    {
        ToastText.Text = message;
        if (!IsVisible) base.Show();
        // 매번 owner 위치 기준으로 가운데 하단 정렬 — owner resize/move 대응.
        SyncToOwner();
        BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation
        {
            To = 1.0,
            Duration = TimeSpan.FromMilliseconds(FadeInMs),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
        _hideTimer.Stop();
        _hideTimer.Start();
    }

    private void FadeOut()
    {
        BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation
        {
            To = 0.0,
            Duration = TimeSpan.FromMilliseconds(FadeOutMs),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        });
    }

    public void SyncToOwner()
    {
        if (Owner is null) return;
        // owner 가운데 하단에서 80px 위.
        var ownerLeft = Owner.Left;
        var ownerTop = Owner.Top;
        var ownerW = Owner.ActualWidth;
        var ownerH = Owner.ActualHeight;
        // Maximized면 RestoreBounds가 의미 없음 — WorkArea로.
        if (Owner.WindowState == WindowState.Maximized)
        {
            var wa = SystemParameters.WorkArea;
            ownerLeft = wa.Left;
            ownerTop = wa.Top;
            ownerW = wa.Width;
            ownerH = wa.Height;
        }
        if (ownerW <= 0 || ownerH <= 0) return;
        UpdateLayout();
        Left = ownerLeft + (ownerW - ActualWidth) / 2;
        Top  = ownerTop  + ownerH - ActualHeight - 80;
    }
}

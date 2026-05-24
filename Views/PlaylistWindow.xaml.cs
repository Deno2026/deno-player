using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using DenoPlayer.Models;
using DenoPlayer.ViewModels;

namespace DenoPlayer.Views;

/// <summary>
/// 메인 윈도우에 owned된 별도 child Window. WPF Popup과 달리 owner와 z-order가
/// 묶이고 owner가 비활성/최소화/닫힘 시 같이 사라져서 다른 앱 위로 새는 문제가 없다.
/// 우측 슬라이드 hover overlay 전용.
/// </summary>
public partial class PlaylistWindow : Window
{
    private readonly DispatcherTimer _hideTimer;
    public bool IsShown { get; private set; }
    public new MainViewModel? DataContext
    {
        get => base.DataContext as MainViewModel;
        set => base.DataContext = value;
    }

    public PlaylistWindow()
    {
        InitializeComponent();
        Width = 320;
        _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
        _hideTimer.Tick += (_, _) => { _hideTimer.Stop(); HideSlide(); };
    }

    public void ShowSlide()
    {
        _hideTimer.Stop();
        if (IsShown) return;
        IsShown = true;
        var slide = new DoubleAnimation
        {
            To = 0,
            Duration = TimeSpan.FromMilliseconds(180),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        SlideTx.BeginAnimation(TranslateTransform.XProperty, slide);

        // 현재 항목 자동 스크롤
        if (DataContext?.CurrentMedia is { } cur)
            Dispatcher.BeginInvoke(new Action(() => PlaylistListBox.ScrollIntoView(cur)),
                DispatcherPriority.Background);
    }

    public void HideSlide()
    {
        if (!IsShown) return;
        IsShown = false;
        var slide = new DoubleAnimation
        {
            To = Width,
            Duration = TimeSpan.FromMilliseconds(160),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        SlideTx.BeginAnimation(TranslateTransform.XProperty, slide);
    }

    public void RequestHideSoon() { _hideTimer.Stop(); _hideTimer.Start(); }

    private void OnPanelEnter(object sender, MouseEventArgs e) => _hideTimer.Stop();
    private void OnPanelLeave(object sender, MouseEventArgs e) => RequestHideSoon();

    private void OnItemDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (PlaylistListBox.SelectedItem is MediaItem mi)
        {
            DataContext?.PlayMedia(mi);
            e.Handled = true;
        }
    }
}

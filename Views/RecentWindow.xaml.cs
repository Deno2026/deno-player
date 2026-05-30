using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using DenoVideoPlayer.Models;
using DenoVideoPlayer.ViewModels;

namespace DenoVideoPlayer.Views;

public partial class RecentWindow : Window
{
    public bool IsShown { get; private set; }
    public event Action<bool>? ShownChanged;
    public new MainViewModel? DataContext
    {
        get => base.DataContext as MainViewModel;
        set => base.DataContext = value;
    }

    // ContextMenu open count — PlaylistWindow와 동일 패턴. 우클릭 메뉴 떴을 때
    // 메인 polling이 IsMouseOver=false라 닫지 않게.
    private int _ctxMenuOpenCount;

    public RecentWindow()
    {
        InitializeComponent();
        Width = 340;
        AddHandler(FrameworkElement.ContextMenuOpeningEvent,
            new ContextMenuEventHandler((_, _) => _ctxMenuOpenCount++), true);
        AddHandler(FrameworkElement.ContextMenuClosingEvent,
            new ContextMenuEventHandler((_, _) => { if (_ctxMenuOpenCount > 0) _ctxMenuOpenCount--; }),
            true);
    }

    public void ShowSlide()
    {
        DataContext?.PruneMissingRecents(save: true);
        if (IsShown) return;
        if (!IsVisible)
        {
            SlideTx.BeginAnimation(TranslateTransform.XProperty, null);
            SlideTx.X = -Width;
            Show();
        }
        IsShown = true;
        ShownChanged?.Invoke(true);
        var slide = new DoubleAnimation
        {
            To = 0,
            Duration = TimeSpan.FromMilliseconds(100),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        SlideTx.BeginAnimation(TranslateTransform.XProperty, slide);
    }

    public void HideSlide()
    {
        if (!IsShown) return;
        if (_ctxMenuOpenCount > 0) return; // 우클릭 메뉴 열린 동안은 닫지 않음
        IsShown = false;
        ShownChanged?.Invoke(false);
        var slide = new DoubleAnimation
        {
            To = -Width,
            Duration = TimeSpan.FromMilliseconds(100),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        slide.Completed += (_, _) =>
        {
            if (!IsShown)
                Hide();
        };
        SlideTx.BeginAnimation(TranslateTransform.XProperty, slide);
    }

    // PlaylistWindow와 동일: 닫힘 판정은 MainWindow.UpdateHotZones에서 일원화.
    // self-close 하면 패널→TopBar로 마우스 올릴 때 닫혀서 위 버튼 조작 불가.
    private void OnPanelEnter(object sender, MouseEventArgs e) { }
    private void OnPanelLeave(object sender, MouseEventArgs e) { /* main window가 판정 */ }

    private void OnItemClicked(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject d)
        {
            var lbi = VisualSearch.FindAncestor<ListBoxItem>(d);
            if (lbi?.Content is RecentItem ri)
            {
                DataContext?.OpenPath(ri.FullPath);
                e.Handled = true;
                // 곡 골랐으니 panel 즉시 접기 (사용자가 mouse 이동 안 해도 자연스럽게)
                HideSlide();
            }
        }
    }

    private void OnClearAll(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not { } vm) return;
        vm.Recents.Clear();
        vm.Settings.RecentFiles = new List<string>();
        vm.SaveSettingsNow();
    }

    private void OnRevealInExplorer(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is RecentItem ri)
            PlaylistWindow.RevealInExplorer(ri.FullPath);
    }

    private void OnCopyPath(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is RecentItem ri)
        {
            try { System.Windows.Clipboard.SetText(ri.FullPath); } catch { }
        }
    }

    private void OnRemoveOne(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is RecentItem ri && DataContext is { } vm)
        {
            vm.RemoveRecent(ri.FullPath, save: true);
        }
    }
}

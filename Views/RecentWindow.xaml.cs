using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using DenoPlayer.Models;
using DenoPlayer.ViewModels;

namespace DenoPlayer.Views;

public partial class RecentWindow : Window
{
    public bool IsShown { get; private set; }
    public event Action<bool>? ShownChanged;
    public new MainViewModel? DataContext
    {
        get => base.DataContext as MainViewModel;
        set => base.DataContext = value;
    }

    public RecentWindow()
    {
        InitializeComponent();
        Width = 340;
    }

    public void ShowSlide()
    {
        if (IsShown) return;
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
        IsShown = false;
        ShownChanged?.Invoke(false);
        var slide = new DoubleAnimation
        {
            To = -Width,
            Duration = TimeSpan.FromMilliseconds(100),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
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
            vm.Recents.Remove(ri);
            vm.Settings.RecentFiles = vm.Recents.Select(r => r.FullPath).ToList();
            vm.SaveSettingsNow();
        }
    }
}

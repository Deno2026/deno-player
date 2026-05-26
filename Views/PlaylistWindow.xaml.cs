using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using DenoPlayer.Models;
using DenoPlayer.ViewModels;

namespace DenoPlayer.Views;

internal static class VisualSearch
{
    public static T? FindAncestor<T>(DependencyObject? d) where T : DependencyObject
    {
        while (d is not null)
        {
            if (d is T t) return t;
            d = VisualTreeHelper.GetParent(d);
        }
        return null;
    }
}

/// <summary>
/// 메인 윈도우에 owned된 별도 child Window. WPF Popup과 달리 owner와 z-order가
/// 묶이고 owner가 비활성/최소화/닫힘 시 같이 사라져서 다른 앱 위로 새는 문제가 없다.
/// 우측 슬라이드 hover overlay 전용.
/// </summary>
public partial class PlaylistWindow : Window
{
    public bool IsShown { get; private set; }
    /// <summary>Show/Hide 시작 시점 통지 — MainWindow가 영상 host margin sync용.</summary>
    public event Action<bool>? ShownChanged;
    public new MainViewModel? DataContext
    {
        get => base.DataContext as MainViewModel;
        set => base.DataContext = value;
    }

    public PlaylistWindow()
    {
        InitializeComponent();
        Width = 320;
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

        if (DataContext?.CurrentMedia is { } cur)
            Dispatcher.BeginInvoke(new Action(() => PlaylistListBox.ScrollIntoView(cur)),
                DispatcherPriority.Background);
    }

    public void HideSlide()
    {
        if (!IsShown) return;
        IsShown = false;
        ShownChanged?.Invoke(false);
        var slide = new DoubleAnimation
        {
            To = Width,
            Duration = TimeSpan.FromMilliseconds(100),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        SlideTx.BeginAnimation(TranslateTransform.XProperty, slide);
    }

    // 패널 닫힘은 MainWindow의 통합 hot zone polling(GetCursorPos 80ms)에서만 처리.
    // 여기서 즉시 HideSlide 호출하면 패널→TopBar로 마우스 이동 중 self-close 발생해서
    // 위쪽 TopBar 버튼 조작이 불가. main window가 inTopBar / IsMouseOver 모두 보고
    // 일관 판정.
    private void OnPanelEnter(object sender, MouseEventArgs e) { /* keep shown */ }
    private void OnPanelLeave(object sender, MouseEventArgs e) { /* main window가 판정 */ }

    private void OnItemClicked(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject d)
        {
            var lbi = VisualSearch.FindAncestor<ListBoxItem>(d);
            if (lbi?.Content is MediaItem mi && DataContext?.CurrentMedia != mi)
            {
                DataContext?.PlayMedia(mi);
                e.Handled = true;
                // 같은 폴더 안에서 곡 바꾸는 경우 panel은 그대로 둠 (사용자가 다른 곡 또
                // 빠르게 고르기 편하게). Recent와 달리 흐름이 끊기지 않음.
            }
        }
    }

    private void OnItemDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (PlaylistListBox.SelectedItem is MediaItem mi)
        {
            DataContext?.PlayMedia(mi);
            e.Handled = true;
        }
    }

    private void OnRevealInExplorer(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is MediaItem mi)
            RevealInExplorer(mi.FullPath);
    }

    private void OnCopyPath(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is MediaItem mi)
        {
            try { System.Windows.Clipboard.SetText(mi.FullPath); } catch { /* clipboard busy */ }
        }
    }

    internal static void RevealInExplorer(string path)
    {
        try
        {
            if (System.IO.File.Exists(path))
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
            else if (System.IO.Directory.Exists(path))
                System.Diagnostics.Process.Start("explorer.exe", $"\"{path}\"");
        }
        catch { /* explorer 못 띄워도 무해 */ }
    }
}

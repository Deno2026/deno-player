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
    private long _acceptItemClicksAfterTick;
    private RecentItem? _pressedItem;
    private readonly SlidePanelMotion _motion;

    public RecentWindow()
    {
        InitializeComponent();
        Width = 340;
        _motion = new SlidePanelMotion(SlideTx, -Width);
        _motion.ResetHidden();
        AddHandler(FrameworkElement.ContextMenuOpeningEvent,
            new ContextMenuEventHandler((_, _) => _ctxMenuOpenCount++), true);
        AddHandler(FrameworkElement.ContextMenuClosingEvent,
            new ContextMenuEventHandler((_, _) => { if (_ctxMenuOpenCount > 0) _ctxMenuOpenCount--; }),
            true);
    }

    public void ShowSlide()
    {
        if (IsShown) return;
        _pressedItem = null;
        if (!IsVisible)
        {
            _motion.ResetHidden();
            Show();
        }
        IsShown = true;
        ShownChanged?.Invoke(true);
        var revealDurationMs = _motion.Reveal();
        _acceptItemClicksAfterTick = Environment.TickCount64 + revealDurationMs + 40;
    }

    public void HideSlide()
    {
        if (!IsShown) return;
        if (_ctxMenuOpenCount > 0) return; // 우클릭 메뉴 열린 동안은 닫지 않음
        _pressedItem = null;
        IsShown = false;
        ShownChanged?.Invoke(false);
        _motion.Conceal(() =>
        {
            if (!IsShown)
                Hide();
        });
    }

    // PlaylistWindow와 동일: 닫힘 판정은 MainWindow.UpdateHotZones에서 일원화.
    // HideSlide는 즉시 시작하되 짧은 slide-out으로 자연스럽게 숨긴다.
    private void OnPanelEnter(object sender, MouseEventArgs e) { }
    private void OnPanelLeave(object sender, MouseEventArgs e) { /* main window가 판정 */ }

    private void OnItemPressed(object sender, MouseButtonEventArgs e)
    {
        _pressedItem = null;
        if (Environment.TickCount64 < _acceptItemClicksAfterTick) return;
        if (e.OriginalSource is DependencyObject d)
            _pressedItem = VisualSearch.FindAncestor<ListBoxItem>(d)?.Content as RecentItem;
    }

    private void OnItemClicked(object sender, MouseButtonEventArgs e)
    {
        // The panel can materialize underneath a click that started on the
        // fullscreen edge. Ignore that release so it cannot open a recent file.
        if (Environment.TickCount64 < _acceptItemClicksAfterTick)
        {
            e.Handled = true;
            return;
        }

        var pressedItem = _pressedItem;
        _pressedItem = null;
        if (pressedItem is null) return;

        if (e.OriginalSource is DependencyObject d)
        {
            var lbi = VisualSearch.FindAncestor<ListBoxItem>(d);
            if (lbi?.Content is RecentItem ri && ReferenceEquals(pressedItem, ri))
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
        vm.ClearRecents();
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

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
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
    public bool IsInteractionActive => _ctxMenuOpenCount > 0 || IsMouseCaptureWithin;
    private long _acceptItemClicksAfterTick;
    private RecentItem? _pressedItem;
    private readonly SlidePanelMotion _motion;
    private int _showSerial;

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
        var showSerial = ++_showSerial;
        _pressedItem = null;
        var needsInitialLayout = !IsVisible;
        if (!IsVisible)
        {
            _motion.ResetHidden();
            Show();
        }
        IsShown = true;
        ShownChanged?.Invoke(true);
        _acceptItemClicksAfterTick = Environment.TickCount64 + 1000;

        if (needsInitialLayout)
        {
            Dispatcher.BeginInvoke(
                new Action(() => RevealAfterLayout(showSerial)),
                DispatcherPriority.Loaded);
            return;
        }

        RevealAfterLayout(showSerial);
    }

    private void RevealAfterLayout(int showSerial)
    {
        if (showSerial != _showSerial || !IsShown || !IsVisible) return;
        RecentListBox.InvalidateMeasure();
        Root.UpdateLayout();
        var revealDurationMs = _motion.Reveal();
        _acceptItemClicksAfterTick = Environment.TickCount64 + revealDurationMs + 40;
    }

    public void HideSlide()
    {
        if (!IsShown) return;
        if (_ctxMenuOpenCount > 0) return; // 우클릭 메뉴 열린 동안은 닫지 않음
        _showSerial++;
        _pressedItem = null;
        IsShown = false;
        ShownChanged?.Invoke(false);
        _motion.Conceal(() =>
        {
            if (!IsShown)
                Hide();
        });
    }

    // Hover 자동 닫기는 MainWindow가 담당한다. 최근 항목 선택 시에도 닫는다.
    private void OnPanelEnter(object sender, MouseEventArgs e) { }
    private void OnPanelLeave(object sender, MouseEventArgs e) { /* explicit toggle */ }

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

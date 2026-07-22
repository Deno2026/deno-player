using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using DenoVideoPlayer.Models;
using DenoVideoPlayer.ViewModels;

namespace DenoVideoPlayer.Views;

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

internal sealed class SlidePanelMotion
{
    public const int ShowDurationMs = 125;
    public const int HideDurationMs = 105;
    private const int MinimumDurationMs = 28;

    private readonly TranslateTransform _translation;
    private readonly double _hiddenX;
    private int _animationSerial;

    public SlidePanelMotion(TranslateTransform translation, double hiddenX)
    {
        _translation = translation;
        _hiddenX = hiddenX;
    }

    public void ResetHidden()
    {
        _animationSerial++;
        _translation.BeginAnimation(TranslateTransform.XProperty, null);
        _translation.X = _hiddenX;
    }

    public int Reveal(Action? completed = null) => AnimateTo(
        targetX: 0,
        durationMs: ShowDurationMs,
        easing: new CubicEase { EasingMode = EasingMode.EaseOut },
        completed);

    public void Conceal(Action completed) => _ = AnimateTo(
        targetX: _hiddenX,
        durationMs: HideDurationMs,
        easing: new CubicEase { EasingMode = EasingMode.EaseOut },
        completed);

    private int AnimateTo(
        double targetX,
        int durationMs,
        IEasingFunction easing,
        Action? completed = null)
    {
        // Read the effective animated values before replacing the animations.
        // A quick edge re-entry then continues from the exact visible position
        // instead of jumping back to a stale base value.
        var currentX = _translation.X;
        var serial = ++_animationSerial;
        var remainingDistance = Math.Abs(targetX - currentX);

        _translation.BeginAnimation(TranslateTransform.XProperty, null);
        _translation.X = targetX;

        if (remainingDistance <= 0.5)
        {
            completed?.Invoke();
            return 0;
        }

        var fullDistance = Math.Max(1.0, Math.Abs(_hiddenX));
        var distanceRatio = Math.Clamp(remainingDistance / fullDistance, 0.0, 1.0);
        var adjustedDurationMs = (int)Math.Round(
            Math.Max(MinimumDurationMs, durationMs * distanceRatio));

        var slide = new DoubleAnimation
        {
            From = currentX,
            To = targetX,
            Duration = TimeSpan.FromMilliseconds(adjustedDurationMs),
            EasingFunction = easing,
            FillBehavior = FillBehavior.Stop
        };
        slide.Completed += (_, _) =>
        {
            if (serial == _animationSerial)
                completed?.Invoke();
        };

        _translation.BeginAnimation(
            TranslateTransform.XProperty,
            slide,
            HandoffBehavior.SnapshotAndReplace);
        return adjustedDurationMs;
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
        set
        {
            if (ReferenceEquals(base.DataContext, value)) return;
            if (base.DataContext is MainViewModel previous)
                previous.PlaylistOrderChanged -= OnPlaylistOrderChanged;
            base.DataContext = value;
            if (value is not null)
                value.PlaylistOrderChanged += OnPlaylistOrderChanged;
        }
    }

    // ContextMenu open count — 메뉴 열린 동안 HideSlide 무시. ContextMenu는 별도
    // popup window라 IsMouseOver=false 됨 → 메인 polling이 닫으려고 함. 그 사이에
    // 사용자가 메뉴 항목 클릭하려는데 패널이 사라져버림.
    private int _ctxMenuOpenCount;
    private long _acceptItemClicksAfterTick;
    private MediaItem? _pressedItem;
    private readonly SlidePanelMotion _motion;

    public PlaylistWindow()
    {
        InitializeComponent();
        Width = 320;
        _motion = new SlidePanelMotion(SlideTx, Width);
        _motion.ResetHidden();
        // ContextMenuOpening/Closing은 bubble 라우티드 이벤트라 Window 레벨에서 catch 가능.
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
        var revealDurationMs = _motion.Reveal(() =>
        {
            if (!IsShown || DataContext?.CurrentMedia is not { } cur) return;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!IsShown || !IsVisible) return;
                if (!ReferenceEquals(DataContext?.CurrentMedia, cur)) return;
                PlaylistListBox.ScrollIntoView(cur);
            }),
                DispatcherPriority.ContextIdle);
        });
        _acceptItemClicksAfterTick = Environment.TickCount64 + revealDurationMs + 40;
    }

    public void HideSlide()
    {
        if (!IsShown) return;
        if (_ctxMenuOpenCount > 0 || PlaylistSortButton.ContextMenu?.IsOpen == true)
            return; // 메뉴 열린 동안은 닫지 않음
        _pressedItem = null;
        IsShown = false;
        ShownChanged?.Invoke(false);
        _motion.Conceal(() =>
        {
            if (!IsShown)
                Hide();
        });
    }

    // 닫힘 판정은 MainWindow의 통합 hot zone polling에서만 처리.
    // HideSlide는 즉시 시작하되 짧은 slide-out으로 자연스럽게 숨긴다.
    private void OnPanelEnter(object sender, MouseEventArgs e) { /* keep shown */ }
    private void OnPanelLeave(object sender, MouseEventArgs e) { /* main window가 판정 */ }

    private void OnSortButtonClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { ContextMenu: { } menu } button) return;
        menu.PlacementTarget = button;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.IsOpen = true;
        e.Handled = true;
    }

    private void OnSortMenuOpened(object sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu menu || DataContext is not { } viewModel) return;
        foreach (var item in menu.Items.OfType<MenuItem>())
            item.IsChecked = item.CommandParameter is PlaylistSortMode mode && mode == viewModel.PlaylistSort;
    }

    private void OnPlaylistOrderChanged()
    {
        if (!IsShown || !IsVisible || DataContext?.CurrentMedia is not { } current) return;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!IsShown || !IsVisible || !ReferenceEquals(DataContext?.CurrentMedia, current)) return;
            PlaylistListBox.ScrollIntoView(current);
        }), DispatcherPriority.ContextIdle);
    }

    private void OnItemPressed(object sender, MouseButtonEventArgs e)
    {
        _pressedItem = null;
        if (Environment.TickCount64 < _acceptItemClicksAfterTick) return;
        if (e.OriginalSource is DependencyObject d)
            _pressedItem = VisualSearch.FindAncestor<ListBoxItem>(d)?.Content as MediaItem;
    }

    private void OnItemClicked(object sender, MouseButtonEventArgs e)
    {
        // The panel can materialize underneath a click that started on the
        // fullscreen edge. Ignore that release so it cannot switch media.
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
            if (lbi?.Content is MediaItem mi && ReferenceEquals(pressedItem, mi) && DataContext?.CurrentMedia != mi)
            {
                DataContext?.PlayMedia(mi);
                e.Handled = true;
                // 같은 폴더 안에서 곡 바꾸는 경우 panel은 그대로 둠 (사용자가 다른 곡 또
                // 빠르게 고르기 편하게). Recent와 달리 흐름이 끊기지 않음.
            }
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

    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is { } viewModel)
            viewModel.PlaylistOrderChanged -= OnPlaylistOrderChanged;
        base.OnClosed(e);
    }
}

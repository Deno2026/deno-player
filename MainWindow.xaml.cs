using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using DenoPlayer.Helpers;
using DenoPlayer.Models;
using DenoPlayer.Services;
using DenoPlayer.ViewModels;

namespace DenoPlayer;

public partial class MainWindow : Window
{
    private readonly MpvProcessService _mpvProc = new();
    private readonly Win32VideoHost _videoHost = new();
    private readonly MainViewModel _vm;
    private readonly DispatcherTimer _controlsHideTimer;
    private readonly DispatcherTimer _playlistHideTimer;
    private bool _wasMaximizedBeforeFs;
    private WindowStyle _savedStyle;
    private ResizeMode _savedResize;
    private double _savedLeft, _savedTop, _savedWidth, _savedHeight;
    private bool _closing;
    private bool _popupsOpen;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel(_mpvProc);
        DataContext = _vm;

        var s = _vm.Settings;
        if (s.WindowWidth >= 480 && s.WindowHeight >= 320)
        {
            Width = s.WindowWidth;
            Height = s.WindowHeight;
        }
        if (s.WindowLeft is { } l && s.WindowTop is { } t)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = l; Top = t;
        }
        if (s.WindowMaximized) WindowState = WindowState.Maximized;
        Topmost = s.AlwaysOnTop;
        _vm.IsAlwaysOnTop = s.AlwaysOnTop;

        VideoHostSlot.Content = _videoHost;

        _controlsHideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(s.ControlAutoHideMs) };
        _controlsHideTimer.Tick += (_, _) => HideControls();

        _playlistHideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000) };
        _playlistHideTimer.Tick += (_, _) => HidePlaylist();

        _mpvProc.Crashed += () => Dispatcher.BeginInvoke(() =>
        {
            if (_closing) return;
            _vm.StatusMessage = "mpv 프로세스가 종료되었습니다.";
        });

        _vm.MouseActivity += () => Dispatcher.BeginInvoke(() =>
        {
            // 영상 hwnd 위 마우스 이동은 mpv에서만 잡힘 → IPC로 받아 OSD 살림
            ShowControls(); RestartHideTimer();
            // 오른쪽 끝 24px 근처면 playlist hot zone 활성
            if (_lastMpvMouseX > 0 && _lastMpvMouseX > ActualWidth - 24 - 2)
                ShowPlaylist();
        });
        _vm.MpvMousePos += (x, y) =>
        {
            _lastMpvMouseX = x; _lastMpvMouseY = y;
        };

        SourceInitialized += OnSourceInit;
        Closing += OnWindowClosing;
        DragEnter += OnDragOver;
        DragOver  += OnDragOver;
        DragLeave += OnDragLeave;
        Drop      += OnDrop;
        KeyDown   += OnAnyKey;
        StateChanged += OnStateChanged;
        LocationChanged += (_, _) => UpdatePopupLayout();

        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.IsFullscreen))
                ApplyFullscreen(_vm.IsFullscreen);
            else if (e.PropertyName == nameof(MainViewModel.IsAlwaysOnTop))
                Topmost = _vm.IsAlwaysOnTop;
        };
    }

    private double _lastMpvMouseX, _lastMpvMouseY;

    public void OpenFromExternal(string path) => _vm.OpenPath(path);

    // ============================================================
    // 초기화 — mpv 프로세스 시작 + IPC 연결
    // ============================================================
    private async void OnSourceInit(object? sender, EventArgs e)
    {
        VideoHostSlot.UpdateLayout();

        if (!_mpvProc.MpvAvailable)
        {
            _vm.State = PlayerState.Failed;
            _vm.StatusMessage =
                $"mpv.exe를 찾을 수 없습니다.\n경로: {_mpvProc.MpvPath}\n" +
                "README의 'mpv 설치' 섹션을 참고하세요.";
            return;
        }

        try
        {
            _mpvProc.Start(_videoHost.Hwnd);
            await _vm.ConnectIpcAsync();
        }
        catch (Exception ex)
        {
            _vm.State = PlayerState.Failed;
            _vm.StatusMessage = "mpv 시작 실패: " + ex.Message;
            return;
        }

        // 명령줄 인자 처리 (Run before IPC가 약간 늦어도 mpv는 idle)
        if (App.StartupArgs.Length > 0)
        {
            var first = App.StartupArgs[0];
            if (File.Exists(first)) _vm.OpenPath(first);
        }

        ShowControls(); RestartHideTimer();
    }

    private void OnRootLoaded(object sender, RoutedEventArgs e)
    {
        // Popup은 PlacementTarget이 화면에 표시된 후에 열어야 위치가 정확
        UpdatePopupLayout();
        OpenPopups();
        UpdatePopupLayout();
    }

    private void OnRootSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdatePopupLayout();
    }

    private void OpenPopups()
    {
        if (_popupsOpen) return;
        TopBarPopup.IsOpen = true;
        BottomBarPopup.IsOpen = true;
        PlaylistHotZonePopup.IsOpen = true;
        PlaylistPanelPopup.IsOpen = true;
        _popupsOpen = true;
    }

    private void UpdatePopupLayout()
    {
        if (!IsLoaded || Root.ActualWidth <= 0 || Root.ActualHeight <= 0) return;
        var rw = Root.ActualWidth;
        var rh = Root.ActualHeight;

        TopBarBorder.Width = rw;
        Reposition(TopBarPopup, 0, 0);

        BottomBarBorder.Width = rw;
        Reposition(BottomBarPopup, 0, rh - BottomBarBorder.Height);

        PlaylistHotZoneBorder.Height = rh;
        Reposition(PlaylistHotZonePopup, rw - PlaylistHotZoneBorder.Width, 0);

        PlaylistPanelBorder.Height = rh;
        Reposition(PlaylistPanelPopup, rw - PlaylistPanelBorder.Width, 0);
    }

    private static void Reposition(Popup p, double x, double y)
    {
        p.HorizontalOffset = x;
        p.VerticalOffset = y;
        // 강제 갱신 trick — 열려있는 popup 위치 즉시 반영
        var h = p.HorizontalOffset;
        p.HorizontalOffset = h + 0.001;
        p.HorizontalOffset = h;
    }

    // ============================================================
    // 닫기
    // ============================================================
    private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _closing = true;
        var (l, t) = (WindowState == WindowState.Normal ? (double?)Left : null,
                       WindowState == WindowState.Normal ? (double?)Top  : null);
        var (w, h) = (WindowState == WindowState.Normal ? Width  : RestoreBounds.Width,
                      WindowState == WindowState.Normal ? Height : RestoreBounds.Height);
        _vm.PersistSettings(w, h, l, t, WindowState == WindowState.Maximized);

        // Popups 닫고 정리
        TopBarPopup.IsOpen = false;
        BottomBarPopup.IsOpen = false;
        PlaylistHotZonePopup.IsOpen = false;
        PlaylistPanelPopup.IsOpen = false;

        _vm.Dispose();
        _mpvProc.Dispose();
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (MaxRestoreBtn != null)
            MaxRestoreBtn.Content = WindowState == WindowState.Maximized ? "" : "";

        // Minimized 시 Popup이 PlacementTarget을 따라가지 않는 WPF 알려진 동작 →
        // 명시적으로 닫고, 복원 시 다시 연다.
        var min = WindowState == WindowState.Minimized;
        if (min)
        {
            TopBarPopup.IsOpen = false;
            BottomBarPopup.IsOpen = false;
            PlaylistHotZonePopup.IsOpen = false;
            PlaylistPanelPopup.IsOpen = false;
        }
        else if (_popupsOpen)
        {
            TopBarPopup.IsOpen = true;
            BottomBarPopup.IsOpen = true;
            PlaylistHotZonePopup.IsOpen = true;
            PlaylistPanelPopup.IsOpen = true;
            UpdatePopupLayout();
        }
    }

    // ============================================================
    // 마우스 자동 숨김
    // ============================================================
    private void OnRootMouseMove(object sender, MouseEventArgs e)
    {
        ShowControls();
        RestartHideTimer();
    }

    private void OnRootMouseLeave(object sender, MouseEventArgs e)
    {
        _controlsHideTimer.Stop();
        HideControls();
    }

    private void OnRootPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        ShowControls();
        RestartHideTimer();
    }

    /// <summary>Popup 내부에서 마우스가 움직여도 OSD 유지</summary>
    private void OnPopupMouseMove(object sender, MouseEventArgs e)
    {
        ShowControls();
        RestartHideTimer();
    }

    private void RestartHideTimer()
    {
        _controlsHideTimer.Stop();
        _controlsHideTimer.Interval = TimeSpan.FromMilliseconds(_vm.Settings.ControlAutoHideMs);
        _controlsHideTimer.Start();
    }

    private void ShowControls()
    {
        FadeTo(TopBarBorder, 1.0, 120);
        FadeTo(BottomBarBorder, 1.0, 120);
        TopBarBorder.IsHitTestVisible = true;
        BottomBarBorder.IsHitTestVisible = true;
        Mouse.OverrideCursor = null;
    }

    private void HideControls()
    {
        if (TopBarBorder.IsMouseOver || BottomBarBorder.IsMouseOver) { RestartHideTimer(); return; }
        if (_vm.Seeking) { RestartHideTimer(); return; }

        FadeTo(TopBarBorder, 0.0, 200, hideAfter: true);
        FadeTo(BottomBarBorder, 0.0, 200, hideAfter: true);
        if (_vm.IsFullscreen)
            Mouse.OverrideCursor = Cursors.None;
    }

    private static void FadeTo(UIElement el, double target, int ms, bool hideAfter = false)
    {
        var anim = new DoubleAnimation
        {
            To = target,
            Duration = TimeSpan.FromMilliseconds(ms),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        if (hideAfter)
            anim.Completed += (_, _) =>
            {
                if (Math.Abs(target) < 0.01) el.IsHitTestVisible = false;
            };
        el.BeginAnimation(UIElement.OpacityProperty, anim);
    }

    // ============================================================
    // Playlist hot zone & panel
    // ============================================================
    private void OnHotZoneEnter(object sender, MouseEventArgs e)
    {
        if (!_vm.Settings.PlaylistPanelEnabled) return;
        ShowPlaylist();
    }

    private void OnPlaylistEnter(object sender, MouseEventArgs e)
    {
        _playlistHideTimer.Stop();
    }

    private void OnPlaylistLeave(object sender, MouseEventArgs e)
    {
        _playlistHideTimer.Stop();
        _playlistHideTimer.Start();
    }

    private void ShowPlaylist()
    {
        PlaylistPanelBorder.IsHitTestVisible = true;
        var slide = new DoubleAnimation
        {
            To = 0,
            Duration = TimeSpan.FromMilliseconds(180),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        var fade = new DoubleAnimation
        {
            To = 1.0,
            Duration = TimeSpan.FromMilliseconds(140)
        };
        PlaylistTx.BeginAnimation(TranslateTransform.XProperty, slide);
        PlaylistPanelBorder.BeginAnimation(UIElement.OpacityProperty, fade);
    }

    private void HidePlaylist()
    {
        _playlistHideTimer.Stop();
        if (PlaylistPanelBorder.IsMouseOver) return;
        var slide = new DoubleAnimation
        {
            To = PlaylistPanelBorder.Width,
            Duration = TimeSpan.FromMilliseconds(160),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        var fade = new DoubleAnimation
        {
            To = 0.0,
            Duration = TimeSpan.FromMilliseconds(160)
        };
        fade.Completed += (_, _) =>
        {
            if (PlaylistPanelBorder.Opacity < 0.05) PlaylistPanelBorder.IsHitTestVisible = false;
        };
        PlaylistTx.BeginAnimation(TranslateTransform.XProperty, slide);
        PlaylistPanelBorder.BeginAnimation(UIElement.OpacityProperty, fade);
    }

    private void OnPlaylistDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (PlaylistListBox.SelectedItem is MediaItem mi)
        {
            _vm.PlayMedia(mi);
            e.Handled = true;
        }
    }

    // ============================================================
    // Seek bar
    // ============================================================
    private void OnSeekDown(object sender, MouseButtonEventArgs e)
    {
        _vm.BeginSeek();
    }

    private void OnSeekUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is Slider sl) _vm.EndSeek(sl.Value);
    }

    private void OnSeekWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Delta > 0) _vm.Seek5ForwardCommand.Execute(null);
        else _vm.Seek5BackwardCommand.Execute(null);
        e.Handled = true;
    }

    private void OnSpeedWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Delta > 0) _vm.IncreaseSpeedCommand.Execute(null);
        else _vm.DecreaseSpeedCommand.Execute(null);
        e.Handled = true;
    }

    private void OnRootMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled) return;
        if (e.Delta > 0) _vm.VolumeUpCommand.Execute(null);
        else _vm.VolumeDownCommand.Execute(null);
        e.Handled = true;
    }

    private void OnRootDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject d &&
            (FindAncestor<Slider>(d) is not null ||
             FindAncestor<Button>(d) is not null ||
             FindAncestor<ListBox>(d) is not null))
            return;
        _vm.FullscreenCommand.Execute(null);
        e.Handled = true;
    }

    private static T? FindAncestor<T>(DependencyObject? d) where T : DependencyObject
    {
        while (d is not null)
        {
            if (d is T t) return t;
            d = VisualTreeHelper.GetParent(d);
        }
        return null;
    }

    // ============================================================
    // Drag & Drop
    // ============================================================
    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
            var ok = files.Any(f =>
                Directory.Exists(f) ||
                (File.Exists(f) && MediaKindExtensions.IsSupported(f)));
            e.Effects = ok ? DragDropEffects.Copy : DragDropEffects.None;
            if (ok && _vm.State != PlayerState.Dragging && _vm.State == PlayerState.NoFile)
                _vm.State = PlayerState.Dragging;
            e.Handled = true;
        }
        else
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
        }
    }

    private void OnDragLeave(object sender, DragEventArgs e)
    {
        if (_vm.State == PlayerState.Dragging)
            _vm.State = PlayerState.NoFile;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
            _vm.TryOpenDroppedFiles(files);
            e.Handled = true;
        }
        if (_vm.State == PlayerState.Dragging)
            _vm.State = PlayerState.NoFile;
    }

    private void OnAnyKey(object sender, KeyEventArgs e)
    {
        ShowControls(); RestartHideTimer();
    }

    // ============================================================
    // Window chrome buttons
    // ============================================================
    private void OnMinimize(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void OnMaxRestore(object? sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void OnClose(object? sender, RoutedEventArgs e) => Close();

    // ============================================================
    // Fullscreen
    // ============================================================
    private void ApplyFullscreen(bool fs)
    {
        if (fs)
        {
            _savedStyle = WindowStyle;
            _savedResize = ResizeMode;
            _wasMaximizedBeforeFs = WindowState == WindowState.Maximized;
            if (!_wasMaximizedBeforeFs)
            {
                _savedLeft = Left; _savedTop = Top;
                _savedWidth = Width; _savedHeight = Height;
            }
            System.Windows.Shell.WindowChrome.SetWindowChrome(this, null);
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            if (WindowState == WindowState.Maximized) WindowState = WindowState.Normal;
            WindowState = WindowState.Maximized;
        }
        else
        {
            Mouse.OverrideCursor = null;
            WindowState = WindowState.Normal;
            WindowStyle = _savedStyle == WindowStyle.None ? WindowStyle.SingleBorderWindow : _savedStyle;
            ResizeMode = _savedResize == ResizeMode.NoResize ? ResizeMode.CanResize : _savedResize;
            System.Windows.Shell.WindowChrome.SetWindowChrome(this, new System.Windows.Shell.WindowChrome
            {
                CaptionHeight = 28,
                ResizeBorderThickness = new Thickness(6),
                GlassFrameThickness = new Thickness(0),
                UseAeroCaptionButtons = false
            });
            if (_wasMaximizedBeforeFs)
                WindowState = WindowState.Maximized;
            else if (_savedWidth > 0 && _savedHeight > 0)
            {
                Left = _savedLeft; Top = _savedTop;
                Width = _savedWidth; Height = _savedHeight;
            }
        }
        UpdatePopupLayout();
    }
}

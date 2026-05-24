using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
    private bool _wasMaximizedBeforeFs;
    private WindowStyle _savedStyle;
    private ResizeMode _savedResize;
    private double _savedLeft, _savedTop, _savedWidth, _savedHeight;
    private bool _closing;

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

        _mpvProc.Crashed += () => Dispatcher.BeginInvoke(() =>
        {
            if (_closing) return;
            _vm.StatusMessage = "mpv 프로세스가 종료되었습니다.";
        });

        // 영상 hwnd 위에서의 마우스 활동은 mpv가 보고해 줘야 잡힘
        _vm.MouseActivity += () => Dispatcher.BeginInvoke(() =>
        {
            ShowControls(); RestartHideTimer();
        });

        SourceInitialized += OnSourceInit;
        Closing += OnWindowClosing;
        DragEnter += OnDragOver;
        DragOver  += OnDragOver;
        DragLeave += OnDragLeave;
        Drop      += OnDrop;
        KeyDown   += OnAnyKey;
        StateChanged += OnStateChanged;

        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.IsFullscreen))
                ApplyFullscreen(_vm.IsFullscreen);
            else if (e.PropertyName == nameof(MainViewModel.IsAlwaysOnTop))
                Topmost = _vm.IsAlwaysOnTop;
        };
    }

    public void OpenFromExternal(string path) => _vm.OpenPath(path);

    // ============================================================
    // 초기화
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

        // 명령줄 인자 처리
        if (App.StartupArgs.Length > 0)
        {
            var first = App.StartupArgs[0];
            if (File.Exists(first)) _vm.OpenPath(first);
        }
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
        _vm.Dispose();
        _mpvProc.Dispose();
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (MaxRestoreBtn != null)
            MaxRestoreBtn.Content = WindowState == WindowState.Maximized ? "" : "";
    }

    // ============================================================
    // TopBar 빈 영역 드래그 + 더블클릭 max/restore
    // ============================================================
    private void OnDragArea_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
            e.Handled = true;
            return;
        }
        try { DragMove(); } catch { /* drag race condition 무시 */ }
    }

    // ============================================================
    // 마우스 자동 숨김 — 풀스크린에서만 동작
    // ============================================================
    /// <summary>풀스크린이 아닐 땐 TopBar/BottomBar 영구 표시.</summary>
    private bool ControlsAlwaysOn => !_vm.IsFullscreen;

    private void OnRootMouseMove(object sender, MouseEventArgs e)
    {
        ShowControls();
        RestartHideTimer();
    }

    private void OnRootMouseLeave(object sender, MouseEventArgs e)
    {
        if (ControlsAlwaysOn) return;
        _controlsHideTimer.Stop();
        HideControls();
    }

    private void OnRootPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        ShowControls();
        RestartHideTimer();
    }

    private void RestartHideTimer()
    {
        _controlsHideTimer.Stop();
        if (ControlsAlwaysOn) return;
        _controlsHideTimer.Interval = TimeSpan.FromMilliseconds(_vm.Settings.ControlAutoHideMs);
        _controlsHideTimer.Start();
    }

    private void ShowControls()
    {
        TopBar.Visibility = Visibility.Visible;
        BottomBar.Visibility = Visibility.Visible;
        FadeTo(TopBar, 1.0, 120);
        FadeTo(BottomBar, 1.0, 120);
        Mouse.OverrideCursor = null;
    }

    private void HideControls()
    {
        if (ControlsAlwaysOn) return;
        if (TopBar.IsMouseOver || BottomBar.IsMouseOver) { RestartHideTimer(); return; }
        if (_vm.Seeking) { RestartHideTimer(); return; }

        FadeTo(TopBar, 0.0, 200, hideAfter: true);
        FadeTo(BottomBar, 0.0, 200, hideAfter: true);
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
                if (Math.Abs(target) < 0.01) el.Visibility = Visibility.Collapsed;
            };
        el.BeginAnimation(UIElement.OpacityProperty, anim);
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
    // Seek bar / speed wheel
    // ============================================================
    private void OnSeekDown(object sender, MouseButtonEventArgs e)
        => _vm.BeginSeek();
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
            // 풀스크린 진입 시 자동 숨김 활성화 — 컨트롤 잠시 후 페이드아웃
            RestartHideTimer();
        }
        else
        {
            Mouse.OverrideCursor = null;
            WindowState = WindowState.Normal;
            WindowStyle = _savedStyle == WindowStyle.None ? WindowStyle.SingleBorderWindow : _savedStyle;
            ResizeMode = _savedResize == ResizeMode.NoResize ? ResizeMode.CanResize : _savedResize;
            System.Windows.Shell.WindowChrome.SetWindowChrome(this, new System.Windows.Shell.WindowChrome
            {
                CaptionHeight = 0,
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
            // 영구 표시 복귀
            ShowControls();
        }
    }
}

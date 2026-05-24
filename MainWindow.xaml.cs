using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
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

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel(_mpvProc);
        DataContext = _vm;

        // 저장된 윈도우 크기/위치 적용
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

        // 영상 호스트 hwnd 부착
        VideoHostSlot.Content = _videoHost;

        _controlsHideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(s.ControlAutoHideMs) };
        _controlsHideTimer.Tick += (_, _) => HideControls();

        _playlistHideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000) };
        _playlistHideTimer.Tick += (_, _) => HidePlaylist();

        // mpv crash 처리
        _mpvProc.Crashed += () => Dispatcher.BeginInvoke(() =>
        {
            if (_closing) return;
            _vm.StatusMessage = "mpv 프로세스가 종료되었습니다.";
        });

        SourceInitialized += OnSourceInit;
        Closing += OnWindowClosing;
        DragEnter += OnDragOver;
        DragOver  += OnDragOver;
        DragLeave += OnDragLeave;
        Drop      += OnDrop;
        KeyDown   += OnAnyKey;
        StateChanged += OnStateChanged;

        // VM의 IsFullscreen / IsAlwaysOnTop 변경 시 window 반영
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.IsFullscreen))
                ApplyFullscreen(_vm.IsFullscreen);
            else if (e.PropertyName == nameof(MainViewModel.IsAlwaysOnTop))
                Topmost = _vm.IsAlwaysOnTop;
        };
    }

    // ============================================================
    // 외부 진입점 (App.OnAppStartup → 명령줄 첫 인자)
    // ============================================================
    public void OpenFromExternal(string path) => _vm.OpenPath(path);

    // ============================================================
    // 초기화 — mpv 프로세스 시작 + IPC 연결
    // ============================================================
    private async void OnSourceInit(object? sender, EventArgs e)
    {
        // hwnd가 BuildWindowCore에서 만들어지도록 layout 한 번 강제
        VideoHostSlot.UpdateLayout();

        // mpv가 없으면 사용자에게 알림 (앱은 살리고 NoFile 유지)
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

        // 시작 시 컨트롤은 잠시 보여줘서 어떤 앱인지 즉시 보이게
        ShowControls(); RestartHideTimer();
    }

    // ============================================================
    // 닫기 — 설정 저장 + mpv 종료
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
        // 마우스 클릭이 발생해도 OSD 살아있어야 함
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
        FadeTo(TopBar, 1.0, 120);
        FadeTo(BottomBar, 1.0, 120);
        TopBar.IsHitTestVisible = true;
        BottomBar.IsHitTestVisible = true;
        Mouse.OverrideCursor = null;
    }

    private void HideControls()
    {
        // 마우스가 컨트롤 바 위에 머무르면 숨기지 않음
        if (TopBar.IsMouseOver || BottomBar.IsMouseOver) { RestartHideTimer(); return; }
        // 시킹/패널 hover 중이면 숨김 보류
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
            EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = EasingMode.EaseOut }
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
        PlaylistPanel.IsHitTestVisible = true;
        var slide = new DoubleAnimation
        {
            To = 0,
            Duration = TimeSpan.FromMilliseconds(180),
            EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = EasingMode.EaseOut }
        };
        var fade = new DoubleAnimation
        {
            To = 1.0,
            Duration = TimeSpan.FromMilliseconds(140)
        };
        PlaylistTx.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, slide);
        PlaylistPanel.BeginAnimation(UIElement.OpacityProperty, fade);
    }

    private void HidePlaylist()
    {
        _playlistHideTimer.Stop();
        if (PlaylistPanel.IsMouseOver) return;
        var slide = new DoubleAnimation
        {
            To = PlaylistPanel.Width,
            Duration = TimeSpan.FromMilliseconds(160),
            EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = EasingMode.EaseIn }
        };
        var fade = new DoubleAnimation
        {
            To = 0.0,
            Duration = TimeSpan.FromMilliseconds(160)
        };
        fade.Completed += (_, _) =>
        {
            if (PlaylistPanel.Opacity < 0.05) PlaylistPanel.IsHitTestVisible = false;
        };
        PlaylistTx.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, slide);
        PlaylistPanel.BeginAnimation(UIElement.OpacityProperty, fade);
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
        var delta = e.Delta > 0 ? 5 : -5;
        // 휠은 즉시 상대 seek
        _ = (delta > 0 ? _vm.Seek5ForwardCommand : _vm.Seek5BackwardCommand);
        if (delta > 0) _vm.Seek5ForwardCommand.Execute(null);
        else _vm.Seek5BackwardCommand.Execute(null);
        e.Handled = true;
    }

    private void OnSpeedWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Delta > 0) _vm.IncreaseSpeedCommand.Execute(null);
        else _vm.DecreaseSpeedCommand.Execute(null);
        e.Handled = true;
    }

    // ============================================================
    // Root wheel — 영상 위에서 휠은 볼륨
    // ============================================================
    private void OnRootMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // BottomBar/SeekBar/Volume 슬라이더 위에서는 이미 처리되었을 것
        if (e.Handled) return;
        if (e.Delta > 0) _vm.VolumeUpCommand.Execute(null);
        else _vm.VolumeDownCommand.Execute(null);
        e.Handled = true;
    }

    // ============================================================
    // Double click — 전체화면 토글
    // ============================================================
    private void OnRootDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // SeekBar/볼륨/버튼 위에서는 토글 안 함
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
            d = System.Windows.Media.VisualTreeHelper.GetParent(d);
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

    // 키 입력 시 OSD 살리기
    private void OnAnyKey(object sender, KeyEventArgs e)
    {
        ShowControls(); RestartHideTimer();
    }

    // ============================================================
    // Window chrome — 우상단 버튼
    // ============================================================
    private void OnMinimize(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void OnMaxRestore(object? sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void OnClose(object? sender, RoutedEventArgs e) => Close();

    // ============================================================
    // Fullscreen — WindowChrome 잠시 제거하고 borderless maximize
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
            // 작업표시줄까지 가리기: WindowState=Normal로 갔다가 Maximized
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
    }
}

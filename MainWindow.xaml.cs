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
using DenoPlayer.Views;

namespace DenoPlayer;

public partial class MainWindow : Window
{
    private readonly MpvProcessService _mpvProc = new();
    private readonly Win32VideoHost _videoHost = new();
    private readonly MainViewModel _vm;
    private readonly DispatcherTimer _controlsHideTimer;
    private WindowStyle _savedStyle;
    private ResizeMode _savedResize;
    private double _savedLeft, _savedTop, _savedWidth, _savedHeight;
    private bool _closing;
    private int _mpvRestartCount;
    private DateTime _lastMpvRestartAt = DateTime.MinValue;
    private const int MaxMpvRestarts = 3;
    private PlaylistWindow? _playlistWin;
    private const int HotZoneWidth = 280;           // 우측 hover trigger — 매우 넓게 (사용자 요청 점진 확대)

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
            // 자동 재시작 — 한 세션에서 일정 한도까지만 (무한 루프 방지)
            if (_mpvRestartCount < MaxMpvRestarts &&
                DateTime.UtcNow - _lastMpvRestartAt > TimeSpan.FromSeconds(5))
            {
                _mpvRestartCount++;
                _lastMpvRestartAt = DateTime.UtcNow;
                _ = RestartMpvAsync();
            }
            else
            {
                _vm.State = PlayerState.Failed;
                _vm.StatusMessage = "mpv 프로세스가 반복 종료되었습니다. 앱을 다시 시작해주세요.";
            }
        });

        // 영상 hwnd 위에서의 마우스 활동은 mpv가 보고해 줘야 잡힘
        _vm.MouseActivity += () => Dispatcher.BeginInvoke(() =>
        {
            ShowControls(); RestartHideTimer();
        });
        // mpv의 hwnd 안 마우스 좌표를 받아 우측 hot-zone 감지 (영상 hwnd 위에서는 WPF가 못 잡음)
        _vm.MpvMousePos += (x, y) => Dispatcher.BeginInvoke(() => CheckRightHotZoneFromMpv(x));
        _vm.Toast       += msg => Dispatcher.BeginInvoke(() => ShowToast(msg));

        SourceInitialized += OnSourceInit;
        Loaded   += OnWindowLoaded;
        Closing += OnWindowClosing;
        DragEnter += OnDragOver;
        DragOver  += OnDragOver;
        DragLeave += OnDragLeave;
        Drop      += OnDrop;
        KeyDown   += OnAnyKey;
        StateChanged += OnStateChanged;
        LocationChanged += (_, _) => SyncPlaylistWindowPosition();
        SizeChanged += (_, _) => SyncPlaylistWindowPosition();

        _vm.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(MainViewModel.IsFullscreen):
                    ApplyFullscreen(_vm.IsFullscreen);
                    break;
                case nameof(MainViewModel.IsAlwaysOnTop):
                    Topmost = _vm.IsAlwaysOnTop;
                    break;
                case nameof(MainViewModel.CurrentMedia):
                    // 재생 항목으로 자동 스크롤은 PlaylistWindow가 ShowSlide 시 직접 처리.
                    break;
                case nameof(MainViewModel.IsPlaylistOpen):
                    if (_vm.IsPlaylistOpen) _playlistWin?.ShowSlide();
                    else _playlistWin?.HideSlide();
                    break;
            }
        };
    }

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

        if (_videoHost.Hwnd == IntPtr.Zero)
        {
            _vm.State = PlayerState.Failed;
            _vm.StatusMessage = "내부 오류: 비디오 호스트 hwnd가 생성되지 않았습니다.";
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

        // 명령줄 인자 처리 — IPC 연결 직후. 이전 인스턴스가 보낸 인자도 여기로 라우팅.
        OpenInitialPathIfAny();
    }

    private async Task RestartMpvAsync()
    {
        try
        {
            _mpvProc.Dispose();
            await Task.Delay(200);
            if (_closing) return;
            _mpvProc.Start(_videoHost.Hwnd);
            await _vm.ConnectIpcAsync();
            _vm.StatusMessage = "";
            // 재생 중이었으면 같은 파일 다시 열기
            if (_vm.CurrentMedia is { } cm) _vm.PlayMedia(cm);
        }
        catch (Exception ex)
        {
            _vm.State = PlayerState.Failed;
            _vm.StatusMessage = "mpv 재시작 실패: " + ex.Message;
        }
    }

    /// <summary>App.StartupArgs / SecondInstanceArgs 첫 인자가 파일이면 열기.</summary>
    private void OpenInitialPathIfAny()
    {
        if (App.StartupArgs.Length > 0)
        {
            var first = App.StartupArgs[0];
            if (File.Exists(first)) _vm.OpenPath(first);
        }
    }

    /// <summary>다른 인스턴스가 인자를 보냄 (single-instance hand-off).</summary>
    public void ReceiveExternalArgs(string[] args)
    {
        Services.AppLog.Info($"ReceiveExternalArgs entry n={args?.Length ?? -1} access={Dispatcher.CheckAccess()}");
        if (args is null || args.Length == 0) return;
        if (Dispatcher.CheckAccess()) ApplyExternalArgs(args);
        else Dispatcher.BeginInvoke(new Action(() =>
        {
            Services.AppLog.Info("BeginInvoke action firing on UI thread");
            ApplyExternalArgs(args);
        }));
    }

    private void ApplyExternalArgs(string[] args)
    {
        Services.AppLog.Info($"ApplyExternalArgs: count={args.Length} first='{(args.Length > 0 ? args[0] : "")}'");
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
        Topmost = true; Topmost = _vm.IsAlwaysOnTop;
        var first = args[0];
        if (File.Exists(first)) _vm.OpenPath(first);
        else Services.AppLog.Warn($"ApplyExternalArgs: file not found '{first}'");
    }

    // ============================================================
    // 닫기
    // ============================================================
    private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _closing = true;
        try { _playlistWin?.Close(); } catch { }
        _playlistWin = null;
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
            MaxRestoreBtn.Content = WindowState == WindowState.Maximized ? "" : "";
        SyncPlaylistWindowPosition();
        // 최소화될 때 playlist도 같이 숨김 (Owner가 minimize면 자동이긴 하지만 명시)
        if (WindowState == WindowState.Minimized) _playlistWin?.HideSlide();
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
        // 영상이 없는 상태(NoFile 등)에선 mpv mouse-pos가 안 와서 WPF 좌표로 hot zone 검사
        CheckRightHotZoneFromWpf(e.GetPosition(Root));
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

    // 현재 OSD 표시 상태 — animation 재트리거 방지 (mpv mouse-pos가 픽셀당 보고)
    private bool _osdShown = true;

    private void ShowControls()
    {
        if (_osdShown && TopBar.Visibility == Visibility.Visible) return;
        _osdShown = true;
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
        if (!_osdShown) return;
        _osdShown = false;

        FadeTo(TopBar, 0.0, 200, hideAfter: true);
        FadeTo(BottomBar, 0.0, 200, hideAfter: true);
        if (_vm.IsFullscreen)
            Mouse.OverrideCursor = Cursors.None;
    }

    // ============================================================
    // Toast — 짧은 OSD confirm (스크린샷 저장 등)
    // ============================================================
    private DispatcherTimer? _toastTimer;
    private void ShowToast(string message)
    {
        ToastText.Text = message;
        var anim = new DoubleAnimation
        {
            To = 1.0,
            Duration = TimeSpan.FromMilliseconds(150),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        ToastBox.BeginAnimation(UIElement.OpacityProperty, anim);

        _toastTimer ??= new DispatcherTimer();
        _toastTimer.Stop();
        _toastTimer.Interval = TimeSpan.FromMilliseconds(2200);
        _toastTimer.Tick -= ToastTimerTick;
        _toastTimer.Tick += ToastTimerTick;
        _toastTimer.Start();
    }
    private void ToastTimerTick(object? sender, EventArgs e)
    {
        _toastTimer?.Stop();
        var anim = new DoubleAnimation
        {
            To = 0.0,
            Duration = TimeSpan.FromMilliseconds(250),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        ToastBox.BeginAnimation(UIElement.OpacityProperty, anim);
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

    // ============================================================
    // Playlist owned window — 우측 hover slide
    // ============================================================
    private void OnWindowLoaded(object? sender, RoutedEventArgs e)
    {
        if (_playlistWin is null)
        {
            _playlistWin = new PlaylistWindow { Owner = this };
            _playlistWin.DataContext = _vm;
            _playlistWin.Show();
            SyncPlaylistWindowPosition();
        }
    }

    private void SyncPlaylistWindowPosition()
    {
        if (_playlistWin is null) return;
        if (WindowState == WindowState.Minimized) return;

        double left, top, w, h;
        if (WindowStyle == WindowStyle.None && WindowState == WindowState.Maximized)
        {
            // 진짜 풀스크린 — Window의 실제 측정값(화면 전체)을 그대로 사용
            left = Left; top = Top; w = ActualWidth; h = ActualHeight;
        }
        else if (WindowState == WindowState.Maximized)
        {
            // 일반 Maximized — 작업표시줄 제외 영역
            var wa = SystemParameters.WorkArea;
            left = wa.Left; top = wa.Top; w = wa.Width; h = wa.Height;
        }
        else
        {
            left = Left; top = Top; w = ActualWidth; h = ActualHeight;
        }

        const double topOffset = 36;
        _playlistWin.Left = left + w - _playlistWin.Width;
        _playlistWin.Top  = top + topOffset;
        _playlistWin.Height = Math.Max(200, h - topOffset - 8);
    }

    /// <summary>WPF 영역(영상 host 외)에서 마우스 위치 → hot zone 안/밖 즉시 반영.</summary>
    private void CheckRightHotZoneFromWpf(Point posInRoot)
    {
        if (_playlistWin is null || _closing) return;
        var w = Root.ActualWidth;
        if (w <= 0) return;
        UpdatePlaylistFromHotZone(posInRoot.X, w);
    }

    /// <summary>mpv가 보고한 영상 hwnd 안 X 좌표 → hot zone 안/밖 즉시 반영.</summary>
    private void CheckRightHotZoneFromMpv(double mpvX)
    {
        if (_playlistWin is null || _closing) return;
        var w = ActualWidth;
        if (w <= 0) return;
        UpdatePlaylistFromHotZone(mpvX, w);
    }

    private void UpdatePlaylistFromHotZone(double x, double w)
    {
        var inHotZone = x >= w - HotZoneWidth;
        if (inHotZone)
        {
            _playlistWin!.ShowSlide();
        }
        else if (_playlistWin!.IsShown && !_playlistWin.IsMouseOver)
        {
            // hot zone 밖이고 panel 위도 아니면 즉시 hide
            _playlistWin.HideSlide();
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
    private void OnSeekDragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
    {
        _vm.BeginSeek();
    }
    private DateTime _lastLiveSeek;
    private void OnSeekDragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        if (sender is not Slider sl) return;
        var now = DateTime.UtcNow;
        if (now - _lastLiveSeek < TimeSpan.FromMilliseconds(80)) return;
        _lastLiveSeek = now;
        _vm.LiveSeek(sl.Value);
    }
    private void OnSeekDragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
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
        // TopBar/BottomBar/재생목록 위 더블클릭은 풀스크린 토글 아님.
        // (TopBar 빈 영역 더블클릭은 OnDragArea_MouseDown에서 max/restore로 이미 처리)
        if (e.OriginalSource is DependencyObject d)
        {
            if (FindAncestor<Slider>(d) is not null) return;
            if (FindAncestor<Button>(d) is not null) return;
            if (FindAncestor<ListBox>(d) is not null) return;
            // TopBar/BottomBar 영역인지 확인
            var parent = d;
            while (parent is not null)
            {
                if (parent == TopBar || parent == BottomBar) return;
                parent = VisualTreeHelper.GetParent(parent);
            }
        }
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
    /// <summary>
    /// Repeat/Shuffle 같은 토글 버튼 클릭 후 ToolTip을 즉시 새 텍스트로 다시 띄움.
    /// WPF ToolTip은 click 시 자동으로 닫혀서 binding은 갱신돼도 보이지 않으니
    /// Command 처리 끝난 다음 cycle에 강제로 다시 IsOpen=true.
    /// </summary>
    private void OnTooltipRefreshClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        if (btn.ToolTip is not System.Windows.Controls.ToolTip tt) return;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                tt.PlacementTarget ??= btn;
                tt.Placement = System.Windows.Controls.Primitives.PlacementMode.Top;
                tt.IsOpen = false;
                // 여전히 마우스가 버튼 위에 있을 때만 다시 표시 — 떠난 뒤면 새로 열지 않음
                if (btn.IsMouseOver) tt.IsOpen = true;
            }
            catch { /* refresh 실패해도 무해 */ }
        }), System.Windows.Threading.DispatcherPriority.Background);
    }

    /// <summary>
    /// 우리가 click 시 IsOpen을 직접 토글하면 WPF의 자동 hide 메커니즘이 끊겨서
    /// 마우스가 떠나도 ToolTip이 ShowDuration 끝까지 남는다. MouseLeave에서 강제 close.
    /// </summary>
    private void OnTooltipMouseLeave(object? sender, MouseEventArgs e)
    {
        if (sender is Button btn && btn.ToolTip is System.Windows.Controls.ToolTip tt)
            tt.IsOpen = false;
    }

    private void OnSettingsClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            var dlg = new SettingsWindow(_vm.Settings) { Owner = this };
            dlg.ShowDialog();
        }
        catch (Exception ex)
        {
            Services.AppLog.Error("Open SettingsWindow", ex);
            MessageBox.Show(this, "환경설정 창을 열 수 없습니다:\n" + ex.Message,
                "Deno Player", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

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
            // 풀스크린 해제는 항상 "창 모드"로 갈 것이므로 normal-mode 좌표를 보존.
            // 현재 Maximized면 RestoreBounds, 아니면 현재 값.
            if (WindowState == WindowState.Maximized)
            {
                var rb = RestoreBounds;
                _savedLeft   = double.IsNaN(rb.Left)   ? 100  : rb.Left;
                _savedTop    = double.IsNaN(rb.Top)    ? 60   : rb.Top;
                _savedWidth  = rb.Width  > 480 ? rb.Width  : 1280;
                _savedHeight = rb.Height > 320 ? rb.Height : 760;
            }
            else
            {
                _savedLeft = Left; _savedTop = Top;
                _savedWidth = Width; _savedHeight = Height;
            }
            System.Windows.Shell.WindowChrome.SetWindowChrome(this, null);
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            if (WindowState == WindowState.Maximized) WindowState = WindowState.Normal;
            WindowState = WindowState.Maximized;
            RestartHideTimer();
        }
        else
        {
            // 사용자 의도가 "풀스크린 ↔ 창 모드 스왑"이므로 Maximized로 자동 복원 안 함.
            // 작은 창으로 명확히 되돌려야 사용자 시각에서 풀스크린과 구분됨.
            Mouse.OverrideCursor = null;
            System.Windows.Shell.WindowChrome.SetWindowChrome(this, new System.Windows.Shell.WindowChrome
            {
                CaptionHeight = 0,
                ResizeBorderThickness = new Thickness(6),
                GlassFrameThickness = new Thickness(0),
                UseAeroCaptionButtons = false
            });
            WindowStyle = _savedStyle == WindowStyle.None ? WindowStyle.SingleBorderWindow : _savedStyle;
            ResizeMode = _savedResize == ResizeMode.NoResize ? ResizeMode.CanResize : _savedResize;
            WindowState = WindowState.Normal;
            if (_savedWidth > 0 && _savedHeight > 0)
            {
                Left = _savedLeft; Top = _savedTop;
                Width = _savedWidth; Height = _savedHeight;
            }
            ShowControls();
        }
        SyncPlaylistWindowPosition();
    }
}

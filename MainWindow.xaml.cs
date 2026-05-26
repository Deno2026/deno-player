using System.IO;
using System.Runtime.InteropServices;
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
    private RecentWindow?   _recentWin;
    private ToastWindow?    _toastWin;
    private const int HotZoneWidth = 180;           // 우측 hover trigger (이전 280 → 2/3 수준)
    private const int LeftHotZoneWidth = 160;       // 좌측 hover trigger (이전 240 → 2/3 수준)


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
            // 멀티 모니터 분리/해상도 변경으로 저장된 좌표가 현재 화면 밖이면 가운데로 강제.
            // 그렇지 않으면 다음 실행 시 창이 보이지 않는 자리에 뜸.
            var vw = SystemParameters.VirtualScreenWidth;
            var vh = SystemParameters.VirtualScreenHeight;
            var vl = SystemParameters.VirtualScreenLeft;
            var vt = SystemParameters.VirtualScreenTop;
            var onScreen = l + 80 < vl + vw && l + Width - 80 > vl &&
                           t + 40 < vt + vh && t + Height - 40 > vt;
            if (onScreen)
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = l; Top = t;
            }
            else
            {
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
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

        // mouse hot zone / mouse activity 둘 다 GetCursorPos polling tick에서 처리.
        _vm.Toast       += msg => Dispatcher.BeginInvoke(() => ShowToast(msg));

        SourceInitialized += OnSourceInit;
        Loaded   += OnWindowLoaded;
        Closing += OnWindowClosing;
        DragEnter += OnDragOver;
        DragOver  += OnDragOver;
        DragLeave += OnDragLeave;
        Drop      += OnDrop;
        KeyDown   += OnAnyKey;
        KeyDown   += OnSpaceDown;
        PreviewKeyUp += OnSpaceUp;
        StateChanged += OnStateChanged;
        LocationChanged += (_, _) => { SyncPlaylistWindowPosition(); SyncRecentWindowPosition(); };
        SizeChanged += (_, _) => { SyncPlaylistWindowPosition(); SyncRecentWindowPosition(); };

        // main window가 deactivate(focus 다른 곳)되면 Space hold timer가 계속 동작해
        // 사용자 의도와 무관하게 2배속 trigger 가능 → timer 중단 + 이미 hold면 복원.
        Deactivated += (_, _) =>
        {
            _spaceHoldTimer?.Stop();
            if (_spaceHeld) { _vm.Speed = _speedBeforeHold; _spaceHeld = false; }
        };

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
                case nameof(MainViewModel.IsTrimMode):
                case nameof(MainViewModel.TrimInSec):
                case nameof(MainViewModel.TrimOutSec):
                case nameof(MainViewModel.Duration):
                    Dispatcher.BeginInvoke(new Action(UpdateTrimOverlay),
                        System.Windows.Threading.DispatcherPriority.Render);
                    break;
            }
        };
        // SeekBar 크기 바뀌면 (창 resize) 핸들 위치 다시 계산
        SizeChanged += (_, _) => UpdateTrimOverlay();
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

        // mouse hot zone polling 시작 (mpv IPC mouse-pos 우회 — 좌표계 신뢰 안 됨)
        StartHotZonePolling();
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
        try { _hotZonePoll?.Stop(); } catch { }
        _hotZonePoll = null;
        try { _playlistWin?.Close(); } catch { }
        try { _recentWin?.Close(); } catch { }
        try { _toastWin?.Close(); } catch { }
        _playlistWin = null;
        _recentWin = null;
        _toastWin = null;
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
            MaxRestoreBtn.Content = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
        SyncPlaylistWindowPosition();
        // 최소화될 때 playlist도 같이 숨김 (Owner가 minimize면 자동이긴 하지만 명시)
        if (WindowState == WindowState.Minimized) _playlistWin?.HideSlide();
    }

    // ============================================================
    // TopBar 빈 영역 드래그 + 더블클릭 max/restore
    // ============================================================
    // 타이틀바 드래그 (단일 click + hold → drag). 더블클릭은 토글 안 함 — 그건
    // OnRootDoubleClick이 단독 처리 (위치 무관 fullscreen 토글).
    // deferred-drag 패턴: MouseDown에서 arm만, MouseMove threshold 넘으면 DragMove.
    // 빠른 더블클릭은 mouse 안 움직여서 DragMove modal loop 발생 X → 두 번째 click 정상 fire.
    private bool _topBarDragArmed;
    private Point _topBarDownPos;

    private void OnTopBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        // 버튼 click은 우리 처리 X
        if (e.OriginalSource is DependencyObject d && FindAncestor<Button>(d) is not null) return;
        // ClickCount==2면 OnRootDoubleClick으로 위임 — 우리는 drag arm 해제만
        if (e.ClickCount >= 2) { _topBarDragArmed = false; return; }
        _topBarDragArmed = true;
        _topBarDownPos = e.GetPosition(this);
    }

    private void OnTopBarMouseMove(object sender, MouseEventArgs e)
    {
        if (!_topBarDragArmed || e.LeftButton != MouseButtonState.Pressed) return;
        var pos = e.GetPosition(this);
        if (Math.Abs(pos.X - _topBarDownPos.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(pos.Y - _topBarDownPos.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        _topBarDragArmed = false;
        try { DragMove(); } catch { /* race 무시 */ }
    }

    private void OnTopBarMouseUp(object sender, MouseButtonEventArgs e) => _topBarDragArmed = false;

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
        // 영상 위(=mpv child hwnd) click 시 OS가 mpv hwnd에 keyboard focus를 줘서
        // WPF KeyBinding이 fire 안 됨 (F/V/Ctrl+S 등 단축키 죽음).
        // click마다 Root grid(Focusable=True)에 focus 강제 — WPF visual tree 안 element가
        // focus 가지면 Window.InputBindings가 keyboard input 받음.
        try { Root.Focus(); Keyboard.Focus(Root); } catch { }
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
    // Toast — owned ToastWindow로 위임 (WPF airspace 우회)
    // ============================================================
    private void ShowToast(string message)
    {
        if (_toastWin is null)
        {
            try { _toastWin = new ToastWindow { Owner = this }; }
            catch { return; }
        }
        _toastWin.Show(message);
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
            _playlistWin.ShownChanged += OnPlaylistShownChanged;
            _playlistWin.Show();
        }
        if (_recentWin is null)
        {
            _recentWin = new RecentWindow { Owner = this };
            _recentWin.DataContext = _vm;
            _recentWin.Show();
        }
        // _toastWin은 lazy create — 첫 ShowToast 시점에 생성. 항상 떠있으면 mouse-pos
        // routing이나 다른 owned window들 layout에 영향 가능.
        SyncPlaylistWindowPosition();
        SyncRecentWindowPosition();
    }

    private void SyncRecentWindowPosition()
    {
        if (_recentWin is null || WindowState == WindowState.Minimized) return;
        DoSyncOnce();
        Dispatcher.BeginInvoke(new Action(DoSyncOnce), DispatcherPriority.ContextIdle);

        void DoSyncOnce()
        {
            if (_recentWin is null) return;
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;
            if (!GetWindowRect(hwnd, out var r)) return;
            var dpi = VisualTreeHelper.GetDpi(this);
            var sx = dpi.DpiScaleX <= 0 ? 1.0 : dpi.DpiScaleX;
            var sy = dpi.DpiScaleY <= 0 ? 1.0 : dpi.DpiScaleY;
            var left = r.Left / sx;
            var top  = r.Top  / sy;
            var w    = (r.Right  - r.Left) / sx;
            var h    = (r.Bottom - r.Top)  / sy;
            if (WindowStyle != WindowStyle.None && WindowState == WindowState.Maximized)
            {
                var wa = SystemParameters.WorkArea;
                if (left < wa.Left) { w -= (wa.Left - left); left = wa.Left; }
                if (top  < wa.Top)  { h -= (wa.Top  - top);  top  = wa.Top;  }
                if (left + w > wa.Right) w = wa.Right - left;
                if (top  + h > wa.Bottom) h = wa.Bottom - top;
            }
            if (w <= 0 || h <= 0) return;
            const double topOffset = 36;
            var bottomReserved = (BottomBar?.ActualHeight ?? 80) + 12;
            _recentWin.Left = left;
            _recentWin.Top  = top + topOffset;
            _recentWin.Height = Math.Max(160, h - topOffset - bottomReserved);
        }
    }

    /// <summary>
    /// 재생목록 panel은 영상 위에 그냥 덮음(사용자 요청). 영상 host 크기는 건드리지 않아서
    /// mpv가 매번 letterbox 재계산할 일이 없음 — 영상 비율/위치 그대로 유지.
    /// 이벤트는 남겨두지만 더 이상 영상 host margin을 만지지 않는다.
    /// </summary>
    private void OnPlaylistShownChanged(bool shown) { /* no-op: panel은 영상 위에 덮음 */ }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hwnd, out PInvokeRect rect);

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct PInvokeRect { public int Left, Top, Right, Bottom; }

    private void SyncPlaylistWindowPosition()
    {
        if (_playlistWin is null) return;
        if (WindowState == WindowState.Minimized) return;

        // 풀스크린/Maximized/멀티모니터/HiDPI에서 WPF Left/ActualWidth가 부정확할 수 있음.
        // hwnd의 실제 화면 좌표를 직접 가져온 뒤 DPI scale로 DIP 변환. 이 값은 어떤 환경에서도 신뢰 가능.
        DoSyncOnce();
        Dispatcher.BeginInvoke(new Action(DoSyncOnce), DispatcherPriority.ContextIdle);

        void DoSyncOnce()
        {
            if (_playlistWin is null) return;

            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;
            if (!GetWindowRect(hwnd, out var r)) return;

            var dpi = VisualTreeHelper.GetDpi(this);
            var sx = dpi.DpiScaleX <= 0 ? 1.0 : dpi.DpiScaleX;
            var sy = dpi.DpiScaleY <= 0 ? 1.0 : dpi.DpiScaleY;

            var left = r.Left / sx;
            var top  = r.Top  / sy;
            var w    = (r.Right  - r.Left) / sx;
            var h    = (r.Bottom - r.Top)  / sy;

            // 일반 Maximized(WindowChrome 적용)는 chrome border만큼 화면 밖으로 살짝 튀어나옴(보통 -8,-8).
            // 작업표시줄까지 가는 진짜 풀스크린이 아니라면 work-area로 클램프해 panel이 작업표시줄 밖으로 안 가게.
            if (WindowStyle != WindowStyle.None && WindowState == WindowState.Maximized)
            {
                var wa = SystemParameters.WorkArea;
                if (left < wa.Left) { w -= (wa.Left - left); left = wa.Left; }
                if (top  < wa.Top)  { h -= (wa.Top  - top);  top  = wa.Top;  }
                if (left + w > wa.Right) w = wa.Right - left;
                if (top  + h > wa.Bottom) h = wa.Bottom - top;
            }

            if (w <= 0 || h <= 0) return;

            const double topOffset = 36;
            var bottomReserved = (BottomBar?.ActualHeight ?? 80) + 12;
            _playlistWin.Left = left + w - _playlistWin.Width;
            _playlistWin.Top  = top + topOffset;
            _playlistWin.Height = Math.Max(160, h - topOffset - bottomReserved);
        }
    }

    /// <summary>WPF 영역(영상 host 외)에서 마우스 위치 → 좌/우 hot zone 둘 다 즉시 반영.</summary>
    private void CheckRightHotZoneFromWpf(Point posInRoot)
    {
        if (_closing) return;
        var w = Root.ActualWidth;
        var h = Root.ActualHeight;
        if (w <= 0 || h <= 0) return;
        UpdateHotZones(posInRoot.X, posInRoot.Y, w, h);
    }

    // ============================================================
    // GetCursorPos polling — mpv IPC mouse-pos는 좌표계 신뢰 어려움(host hwnd 안
    // native pixel/logical/video pixel 어떤지 환경마다 다름) + WPF MouseMove는 HwndHost
    // 위에서 fire 안 됨. Win32 GetCursorPos로 직접 screen coord 받아 main window
    // 안 logical 좌표 변환 + hot zone 검사. 80ms tick은 60Hz 영상에 충분.
    // ============================================================
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out PinvokePoint lpPoint);
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct PinvokePoint { public int X; public int Y; }

    private DispatcherTimer? _hotZonePoll;
    private Point _lastPollMouse;

    private void StartHotZonePolling()
    {
        if (_hotZonePoll is not null) return;
        _hotZonePoll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
        _hotZonePoll.Tick += OnHotZonePollTick;
        _hotZonePoll.Start();
    }

    private void OnHotZonePollTick(object? sender, EventArgs e)
    {
        if (_closing) return;
        if (!IsActive)
        {
            // 다른 앱이 foreground면 panel은 mouse-over 아닐 때 닫음.
            if (_playlistWin?.IsShown == true && !_playlistWin.IsMouseOver) _playlistWin.HideSlide();
            if (_recentWin?.IsShown == true && !_recentWin.IsMouseOver) _recentWin.HideSlide();
            return;
        }
        if (!GetCursorPos(out var pt)) return;
        var src = System.Windows.PresentationSource.FromVisual(this);
        if (src?.CompositionTarget is null) return;

        // Screen native pixel → WPF DIP (logical) — DPI-aware.
        var fromDevice = src.CompositionTarget.TransformFromDevice;
        var ptLogical = fromDevice.Transform(new Point(pt.X, pt.Y));

        // main window-relative.
        var winLeft = WindowState == WindowState.Maximized
            ? (Left < -10 ? RestoreBounds.Left : Left) // Maximized면 Left 음수 가능
            : Left;
        var winTop = WindowState == WindowState.Maximized
            ? (Top < -10 ? RestoreBounds.Top : Top)
            : Top;
        // 위 단순화: Maximized 시 chrome border만큼 음수일 수 있어 WorkArea로 클램프
        // 가 더 정확하지만 일단 SystemParameters.WorkArea로:
        if (WindowState == WindowState.Maximized)
        {
            var wa = System.Windows.SystemParameters.WorkArea;
            winLeft = wa.Left;
            winTop = wa.Top;
        }
        var relX = ptLogical.X - winLeft;
        var relY = ptLogical.Y - winTop;
        var w = WindowState == WindowState.Maximized
            ? System.Windows.SystemParameters.WorkArea.Width
            : ActualWidth;
        var h = WindowState == WindowState.Maximized
            ? System.Windows.SystemParameters.WorkArea.Height
            : ActualHeight;
        if (w <= 0 || h <= 0) return;

        // Mouse 움직임 감지 → ShowControls (이전엔 mpv MouseActivity event가 담당).
        // 풀스크린 모드에서 마우스 움직일 때 OSD 다시 보이게 하는 게 핵심.
        if (Math.Abs(ptLogical.X - _lastPollMouse.X) > 0.5 ||
            Math.Abs(ptLogical.Y - _lastPollMouse.Y) > 0.5)
        {
            _lastPollMouse = ptLogical;
            if (relX >= 0 && relX < w && relY >= 0 && relY < h)
            {
                ShowControls();
                RestartHideTimer();
            }
        }

        UpdateHotZones(relX, relY, w, h);
    }

    private void UpdateHotZones(double x, double y, double w, double h)
    {
        // 하단 transport bar 영역은 hot zone에서 제외.
        var bottomReserved = (BottomBar?.ActualHeight ?? 80) + 12;
        var inLowerStrip = y >= h - bottomReserved;
        // 좌측 패널(최근 재생)은 화면 세로 상반부에서만 활성. 작업 중 무심코 좌측
        // 내리다가 panel이 튀어나오는 것 방지.
        var inUpperHalf = y < h / 2;
        // 패널 보호 영역: TopBar(상단 버튼들) 위에 마우스가 있을 때는 이미 열린 패널을
        // 닫지 않음. 안 그러면 패널 열고 우측의 환경설정/스크린샷/창 버튼 만지려고
        // 위로 갈 때 hot zone 벗어났다고 패널이 닫혀버려서 조작감 안 좋음.
        var inTopBar = y < (TopBar?.ActualHeight ?? 36);

        if (_playlistWin is not null)
        {
            // 좌측과 일관성 — 우측도 세로 상단 절반 + 하단 transport 영역 제외에서만 trigger.
            var inRight = x >= w - HotZoneWidth && !inLowerStrip && inUpperHalf;
            if (inRight) _playlistWin.ShowSlide();
            else if (_playlistWin.IsShown && !_playlistWin.IsMouseOver && !inTopBar)
                _playlistWin.HideSlide();
        }
        if (_recentWin is not null)
        {
            var inLeft = x >= 0 && x < LeftHotZoneWidth && !inLowerStrip && inUpperHalf;
            if (inLeft) _recentWin.ShowSlide();
            else if (_recentWin.IsShown && !_recentWin.IsMouseOver && !inTopBar)
                _recentWin.HideSlide();
        }
    }

    // ============================================================
    // Seek bar / volume slider — mouse capture + 직접 1:1 mapping.
    //
    // 사용자 요구: "클릭한 지점으로 바로 강제 이동 + 그 기준 anchor 따라가게".
    // WPF Thumb 기본 drag는 click offset 유지 (mouse가 thumb 잡은 그 지점이 anchor).
    // 사용자 시각에선 어색 → mouse position 자체를 직접 Slider.Value로 mapping.
    // Slider style의 IsMoveToPointEnabled / Thumb DragDelta는 더 이상 사용 안 함.
    // ============================================================
    private bool _seekDragging;
    private bool _volDragging;
    private DateTime _lastLiveSeek;

    private void OnSeekDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Slider sl) return;
        // 편집 모드일 땐 SeekBar drag/click 막음 — IN/OUT 핸들만 사용
        if (_vm.IsTrimMode) { e.Handled = true; return; }
        sl.CaptureMouse();
        _seekDragging = true;
        _vm.BeginSeek();
        ApplyMousePosToSlider(sl, e.GetPosition(sl)); // 즉시 클릭 지점으로 점프
        _vm.LiveSeek(sl.Value);
        e.Handled = true;
    }

    private void OnSeekMove(object sender, MouseEventArgs e)
    {
        if (!_seekDragging || sender is not Slider sl) return;
        ApplyMousePosToSlider(sl, e.GetPosition(sl));
        var now = DateTime.UtcNow;
        if (now - _lastLiveSeek < TimeSpan.FromMilliseconds(80)) return;
        _lastLiveSeek = now;
        _vm.LiveSeek(sl.Value);
    }

    private void OnSeekUp(object sender, MouseButtonEventArgs e)
    {
        if (!_seekDragging || sender is not Slider sl) return;
        sl.ReleaseMouseCapture();
        _seekDragging = false;
        ApplyMousePosToSlider(sl, e.GetPosition(sl));
        Services.AppLog.Info($"OnSeekUp: final value={sl.Value:F2}");
        _vm.EndSeek(sl.Value);
        e.Handled = true;
    }

    private void OnVolumeSliderDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Slider sl) return;
        sl.CaptureMouse();
        _volDragging = true;
        ApplyMousePosToSlider(sl, e.GetPosition(sl));
        e.Handled = true;
    }

    private void OnVolumeSliderMove(object sender, MouseEventArgs e)
    {
        if (!_volDragging || sender is not Slider sl) return;
        ApplyMousePosToSlider(sl, e.GetPosition(sl));
    }

    private void OnVolumeSliderUp(object sender, MouseButtonEventArgs e)
    {
        if (!_volDragging || sender is not Slider sl) return;
        sl.ReleaseMouseCapture();
        _volDragging = false;
        ApplyMousePosToSlider(sl, e.GetPosition(sl));
        e.Handled = true;
    }

    /// <summary>
    /// mouse X 좌표를 Slider 가로 비율로 변환해 Value로 직접 설정. anchor offset 없이 1:1.
    /// VolumeSlider는 TwoWay binding이라 sl.Value = ...로 set해야 ViewModel.Volume까지 전파.
    /// SeekBar는 OneWay binding이라 SetCurrentValue로 시각만 변경 (mpv time-pos 흐름 유지).
    /// </summary>
    private static void ApplyMousePosToSlider(Slider sl, Point pos)
    {
        if (sl.ActualWidth <= 0) return;
        var ratio = Math.Clamp(pos.X / sl.ActualWidth, 0, 1);
        var value = sl.Minimum + ratio * (sl.Maximum - sl.Minimum);
        // SeekBar는 OneWay (Mode=OneWay) — local set이 binding 끊으니 SetCurrentValue 사용.
        // VolumeSlider는 TwoWay (default) — sl.Value = value로 ViewModel.Volume까지 push.
        var bindExpr = sl.GetBindingExpression(Slider.ValueProperty);
        if (bindExpr is { ParentBinding.Mode: System.Windows.Data.BindingMode.OneWay })
            sl.SetCurrentValue(Slider.ValueProperty, value);
        else
            sl.Value = value;
    }
    private void OnSeekWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Delta > 0) _vm.Seek5ForwardCommand.Execute(null);
        else _vm.Seek5BackwardCommand.Execute(null);
        e.Handled = true;
    }
    // ============================================================
    // Trim 편집 모드 — SeekBar 위 IN/OUT 핸들 드래그
    // ============================================================
    // SeekBar Slider 내부 thumb 크기 (SeekThumb style) — Track 사용 영역 보정용.
    // Slider Value=Min일 때 thumb 중앙이 thumbHalf(=8)에서 시작, Value=Max일 때
    // ActualWidth-thumbHalf에 도달. 그 사이 거리(usableWidth)가 실제 sec 매핑 영역.
    private const double SeekThumbWidth = 16.0;
    private const double SeekThumbHalf  = SeekThumbWidth / 2.0;
    // 우리 trim 핸들 크기 — XAML과 일치 (18px)
    private const double TrimHandleWidth = 18.0;
    private const double TrimHandleHalf  = TrimHandleWidth / 2.0;

    /// <summary>sec → SeekBar 좌표(핸들 중앙 X) 변환.</summary>
    private double SecToX(double sec)
    {
        var usable = Math.Max(0, SeekBar.ActualWidth - SeekThumbWidth);
        if (_vm.Duration <= 0 || usable <= 0) return SeekThumbHalf;
        return SeekThumbHalf + (sec / _vm.Duration) * usable;
    }

    /// <summary>px 변화(드래그 delta) → sec 변화. usableWidth 기준.</summary>
    private double PxDeltaToSec(double dxPx)
    {
        var usable = Math.Max(1, SeekBar.ActualWidth - SeekThumbWidth);
        return dxPx * (_vm.Duration / usable);
    }

    private void OnTrimInDragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
    {
        // BeginSeek → Seeking=true → mpv가 time-pos 옛 값으로 덮어쓰지 못하게 차단
        _vm.BeginSeek();
    }
    private void OnTrimInDrag(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        if (_vm.Duration <= 0 || SeekBar.ActualWidth <= 0) return;
        var deltaSec = PxDeltaToSec(e.HorizontalChange);
        var cur = _vm.TrimInSec ?? 0;
        var max = (_vm.TrimOutSec ?? _vm.Duration) - 0.1;
        var newIn = Math.Clamp(cur + deltaSec, 0, max);
        _vm.TrimInSec = newIn;
        UpdateTrimOverlay();
        // IN 위치로 재생 live seek (80ms throttle, mpv 부담 완화)
        var now = DateTime.UtcNow;
        if (now - _lastLiveSeek >= TimeSpan.FromMilliseconds(80))
        {
            _lastLiveSeek = now;
            _vm.LiveSeek(newIn);
        }
    }
    private void OnTrimInDragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        // 마지막 위치로 한 번 더 seek 보장 (throttle에 막혔을 수 있음) + Seeking=false
        _vm.EndSeek(_vm.TrimInSec ?? 0);
    }
    private void OnTrimOutDrag(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        if (_vm.Duration <= 0 || SeekBar.ActualWidth <= 0) return;
        var deltaSec = PxDeltaToSec(e.HorizontalChange);
        var cur = _vm.TrimOutSec ?? _vm.Duration;
        var min = (_vm.TrimInSec ?? 0) + 0.1;
        _vm.TrimOutSec = Math.Clamp(cur + deltaSec, min, _vm.Duration);
        UpdateTrimOverlay();
    }

    /// <summary>SeekBar 위 IN/OUT 핸들 좌표 + range 강조 위치/크기 갱신.
    /// Slider thumb track 영역 보정 → IN=0이면 핸들 중앙이 SeekBar 시작 thumb 위치와 일치.</summary>
    private void UpdateTrimOverlay()
    {
        if (TrimOverlay is null || SeekBar is null) return;
        if (!_vm.IsTrimMode || _vm.Duration <= 0) return;
        if (SeekBar.ActualWidth <= 0) return;

        var inCenterX  = SecToX(_vm.TrimInSec  ?? 0);
        var outCenterX = SecToX(_vm.TrimOutSec ?? _vm.Duration);

        // 핸들 중앙이 SeekBar thumb 중앙과 정확히 일치하도록 좌측 좌표 보정
        System.Windows.Controls.Canvas.SetLeft(TrimInThumb,  inCenterX  - TrimHandleHalf);
        System.Windows.Controls.Canvas.SetLeft(TrimOutThumb, outCenterX - TrimHandleHalf);
        // Range fill — 두 핸들 중앙 사이
        System.Windows.Controls.Canvas.SetLeft(TrimRangeFill, inCenterX);
        TrimRangeFill.Width = Math.Max(0, outCenterX - inCenterX);
    }

    private void OnSpeedWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Delta > 0) _vm.IncreaseSpeedCommand.Execute(null);
        else _vm.DecreaseSpeedCommand.Execute(null);
        e.Handled = true;
    }
    private void OnSpeedButtonClick(object sender, RoutedEventArgs e)
    {
        // 좌클릭 시 attached ContextMenu(=speed preset 메뉴) 펼치기. YouTube/VLC 패턴.
        if (sender is Button b && b.ContextMenu is not null)
        {
            b.ContextMenu.PlacementTarget = b;
            b.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Top;
            b.ContextMenu.IsOpen = true;
        }
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
        // 단순 규칙: 어디서 더블클릭하든 Fullscreen 토글. 컨트롤 요소(슬라이더/버튼/
        // 리스트박스/BottomBar)만 제외. 사용자가 짚어준 정확한 의도 — 위치 무관, 현재
        // 상태(전체/아님) 기준으로만.
        if (e.OriginalSource is DependencyObject d)
        {
            if (FindAncestor<Slider>(d) is not null) return;
            if (FindAncestor<Button>(d) is not null) return;
            if (FindAncestor<ListBox>(d) is not null) return;
            var p = d;
            while (p is not null)
            {
                if (p == BottomBar) return;
                p = VisualTreeHelper.GetParent(p);
            }
        }
        Services.AppLog.Info($"DblClick → Fullscreen toggle (fs={_vm.IsFullscreen})");
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
            // 미디어, 폴더, 또는 자막(현재 영상에 add-sub 의도) — 셋 중 하나라도 있으면 accept
            var ok = files.Any(f =>
                Directory.Exists(f) ||
                (File.Exists(f) && (MediaKindExtensions.IsSupported(f) || MediaKindExtensions.IsSubtitle(f))));
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
    // Space: 짧게 누름 = Play/Pause, 1초 이상 hold = 2x 재생 (놓으면 원래 속도)
    // 사용자 요구: IsRepeat(OS 키 auto-repeat ~500ms 가변) 대신 정확히 1초 timer.
    // ============================================================
    private bool _spaceHeld;
    private double _speedBeforeHold = 1.0;
    private DispatcherTimer? _spaceHoldTimer;

    private void OnSpaceDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Space) return;
        ShowControls(); RestartHideTimer();
        e.Handled = true;
        if (e.IsRepeat) return; // OS auto-repeat — 첫 keydown만 timer 시작

        // 1초 후 hold = true + 2x. 그 전에 KeyUp 들어오면 단발 play/pause.
        // YouTube와 같이 0.5초 후 2배속 시작 (그 전 release면 단순 play/pause toggle).
        _spaceHoldTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _spaceHoldTimer.Tick -= OnSpaceHoldTick;
        _spaceHoldTimer.Tick += OnSpaceHoldTick;
        _spaceHoldTimer.Start();
    }

    private void OnSpaceHoldTick(object? sender, EventArgs e)
    {
        _spaceHoldTimer?.Stop();
        if (_spaceHeld) return;
        _spaceHeld = true;
        _speedBeforeHold = _vm.Speed;
        _vm.Speed = 2.0;
        ShowControls(); RestartHideTimer();
    }

    private void OnSpaceUp(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Space) return;
        e.Handled = true;
        EndSpaceHoldOrToggle();
    }

    /// <summary>1초 안에 release → play/pause 단발. 1초 후 release → speed 복원.</summary>
    private void EndSpaceHoldOrToggle()
    {
        _spaceHoldTimer?.Stop();
        if (_spaceHeld)
        {
            _vm.Speed = _speedBeforeHold;
            _spaceHeld = false;
        }
        else
        {
            _vm.PlayPauseCommand.Execute(null);
        }
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
    // 풀스크린 진입/탈출 — 200ms cubic ease 애니메이션으로 부드럽게 grow/shrink.
    // 핵심:
    //   - WindowChrome SetWindowChrome 호출 안 함 (XAML 한 번 set으로 유지)
    //   - WindowStyle 변경은 진입 시 한 번 (chrome 사라짐), 탈출 시 animate 종료 후 복원
    //   - Bounds 4개 (Left/Top/Width/Height)를 DoubleAnimation으로 동시에 interpolate
    //     → WPF가 매 frame layout, mpv child hwnd가 자연스럽게 따라옴
    //   - WindowState 토글 안 함 (Maximized hack 제거)
    private void ApplyFullscreen(bool fs)
    {
        _playlistWin?.HideSlide();
        _recentWin?.HideSlide();

        if (fs)
        {
            try { Root.Focus(); Keyboard.Focus(Root); } catch { }
            _savedStyle = WindowStyle;
            _savedResize = ResizeMode;
            _savedWasMaximized = WindowState == WindowState.Maximized;
            if (_savedWasMaximized)
            {
                var rb = RestoreBounds;
                _savedLeft   = double.IsNaN(rb.Left)   ? 100  : rb.Left;
                _savedTop    = double.IsNaN(rb.Top)    ? 60   : rb.Top;
                _savedWidth  = rb.Width  > 480 ? rb.Width  : 1280;
                _savedHeight = rb.Height > 320 ? rb.Height : 760;
                // Maximized면 RestoreBounds로 visual 위치를 먼저 잡고 거기서 시작.
                // 안 그러면 Maximized의 가상 bounds(-8,-8,fullW+16,fullH+16)에서
                // 모니터로 점프 → 어색.
                WindowState = WindowState.Normal;
                Left = _savedLeft; Top = _savedTop;
                Width = _savedWidth; Height = _savedHeight;
            }
            else
            {
                _savedLeft = Left; _savedTop = Top;
                _savedWidth = Width; _savedHeight = Height;
            }

            // chrome 제거 (한 번의 reflow). 그 후 bounds animate.
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;

            var mb = GetCurrentMonitorBounds();
            AnimateBounds(mb.X, mb.Y, mb.Width, mb.Height, durationMs: 140);
            RestartHideTimer();
        }
        else
        {
            Mouse.OverrideCursor = null;
            // 먼저 bounds를 saved로 animate, 끝난 후 WindowStyle 복원
            var tx = _savedLeft;
            var ty = _savedTop;
            var tw = _savedWidth  > 0 ? _savedWidth  : 1280;
            var th = _savedHeight > 0 ? _savedHeight : 760;
            AnimateBounds(tx, ty, tw, th, durationMs: 140, onCompleted: () =>
            {
                WindowStyle = _savedStyle == WindowStyle.None ? WindowStyle.SingleBorderWindow : _savedStyle;
                ResizeMode = _savedResize == ResizeMode.NoResize ? ResizeMode.CanResize : _savedResize;
                if (_savedWasMaximized) WindowState = WindowState.Maximized;
            });
            ShowControls();
        }
        SyncPlaylistWindowPosition();
        SyncRecentWindowPosition();
    }

    /// <summary>
    /// 윈도우 Bounds (Left/Top/Width/Height)를 부드럽게 animate. mpv child hwnd가 매
    /// frame 따라가서 grow/shrink 자연스러움. 이전 animation 진행 중이면 cancel하고
    /// 현재 값에서 새로 시작.
    /// </summary>
    private void AnimateBounds(double targetX, double targetY, double targetW, double targetH,
        double durationMs, Action? onCompleted = null)
    {
        var dur = new Duration(TimeSpan.FromMilliseconds(durationMs));
        var ease = new System.Windows.Media.Animation.CubicEase
            { EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut };

        var animL = new System.Windows.Media.Animation.DoubleAnimation(targetX, dur) { EasingFunction = ease };
        var animT = new System.Windows.Media.Animation.DoubleAnimation(targetY, dur) { EasingFunction = ease };
        var animW = new System.Windows.Media.Animation.DoubleAnimation(targetW, dur) { EasingFunction = ease };
        var animH = new System.Windows.Media.Animation.DoubleAnimation(targetH, dur) { EasingFunction = ease };

        // FillBehavior.Stop + 완료 콜백에서 실제 값을 set해야 애니메이션이 끝난 후에도
        // 좌표가 유지됨 (안 그러면 WPF가 animation snapshot으로 freeze).
        animL.FillBehavior = System.Windows.Media.Animation.FillBehavior.Stop;
        animT.FillBehavior = System.Windows.Media.Animation.FillBehavior.Stop;
        animW.FillBehavior = System.Windows.Media.Animation.FillBehavior.Stop;
        animH.FillBehavior = System.Windows.Media.Animation.FillBehavior.Stop;

        animH.Completed += (_, _) =>
        {
            BeginAnimation(LeftProperty, null);
            BeginAnimation(TopProperty, null);
            BeginAnimation(WidthProperty, null);
            BeginAnimation(HeightProperty, null);
            Left = targetX; Top = targetY;
            Width = targetW; Height = targetH;
            onCompleted?.Invoke();
        };

        BeginAnimation(LeftProperty, animL);
        BeginAnimation(TopProperty, animT);
        BeginAnimation(WidthProperty, animW);
        BeginAnimation(HeightProperty, animH);
    }

    private bool _savedWasMaximized;

    // 현재 윈도우가 있는 모니터의 전체 bounds (DPI scale 적용된 DIP 단위).
    private Rect GetCurrentMonitorBounds()
    {
        try
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            var mh = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            var mi = new MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFO>() };
            if (GetMonitorInfo(mh, ref mi))
            {
                var src = System.Windows.PresentationSource.FromVisual(this);
                var dpiX = src?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
                var dpiY = src?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;
                var r = mi.rcMonitor;
                return new Rect(r.left / dpiX, r.top / dpiY,
                    (r.right - r.left) / dpiX, (r.bottom - r.top) / dpiY);
            }
        }
        catch (Exception ex)
        {
            Services.AppLog.Warn($"GetCurrentMonitorBounds failed: {ex.Message}");
        }
        return new Rect(0, 0, SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight);
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct WIN32RECT { public int left, top, right, bottom; }
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public WIN32RECT rcMonitor;
        public WIN32RECT rcWork;
        public uint dwFlags;
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);
    private const uint MONITOR_DEFAULTTONEAREST = 2;
}

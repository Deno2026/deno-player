using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using DenoVideoPlayer.Helpers;
using DenoVideoPlayer.Models;
using DenoVideoPlayer.Services;
using DenoVideoPlayer.ViewModels;
using DenoVideoPlayer.Views;

namespace DenoVideoPlayer;

public partial class MainWindow : Window
{
    private readonly MpvProcessService _mpvProc = new();
    private readonly Win32VideoHost _videoHost = new();
    private readonly MainViewModel _vm;
    private readonly CancellationTokenSource _runtimePrepareCts = new();
    private ResizeMode _savedResize = ResizeMode.CanResize;
    private double _savedLeft, _savedTop, _savedWidth, _savedHeight;
    private bool _hasFullscreenRestoreBounds;
    private bool _closing;
    private bool _mpvStartupOrRecovery;
    private int _backendOperationInFlight;
    private string? _pendingOpenPath;
    private int _mpvRestartCount;
    private DateTime _lastMpvRestartAt = DateTime.MinValue;
    private const int MaxMpvRestarts = 3;
    private PlaylistWindow? _playlistWin;
    private RecentWindow?   _recentWin;
    private ToastWindow?    _toastWin;
    private HelpWindow?     _helpWin;
    private const double FullscreenStationaryRevealThreshold = 16.0;
    private const double WindowResizeBorderDip = 6.0;
    private const int VkLButton = 0x01;
    private const int VkMButton = 0x04;
    private const int VkControl = 0x11;
    private const int FullscreenDoubleClickDuplicateSuppressMs = 650;
    private const int WindowButtonDebounceMs = 320;
    private bool _pollLeftButtonWasDown;
    private bool _pollMiddleButtonWasDown;
    private bool _ignorePollUntilLeftReleased;
    private DateTime _lastPollLeftDownUtc = DateTime.MinValue;
    private DateTime _suppressPollClickUntilUtc = DateTime.MinValue;
    private DateTime _lastDoubleClickToggleUtc = DateTime.MinValue;
    private DateTime _lastMaxRestoreClickUtc = DateTime.MinValue;
    private Point _lastPollLeftDownPos;
    private bool _ignoreStationaryFullscreenReveal;
    private Point _fullscreenHiddenAtMouse;
    private int _boundsAnimationSerial;
    private bool _updatePromptOpen;
    private bool _ownedModalOpen;
    private bool _videoPanDragging;
    private bool _videoPanUsesPolledButtonState;
    private bool _videoPanSawAsyncButtonDown;
    private int _videoPanAsyncReleaseMisses;
    private Point _videoPanLastScreenPoint;
    private Point _videoPanStartPoint;
    private double _videoPanStartX;
    private double _videoPanStartY;
    private double _videoScaleCurrent = 1.0;
    private double _videoScaleTarget = 1.0;
    private double _videoPanXCurrent;
    private double _videoPanYCurrent;
    private double _videoPanXTarget;
    private double _videoPanYTarget;
    private Point _queuedVideoPanScreenPoint;
    private string _queuedVideoPanSource = "video-pan";
    private int _videoPanDispatchScheduled;
    private DateTime _lastVideoPanKeepAliveUtc = DateTime.MinValue;
    private IntPtr _mouseWheelHook;
    private LowLevelMouseProc? _mouseWheelHookProc;
    private int _queuedVideoWheelDelta;
    private int _videoWheelDispatchScheduled;
    private int _volumeWheelRemainder;
    private int _overlaySyncScheduled;
    private bool _audioAnimationRunning;
    private bool _loadingAnimationRunning;
    private const double VideoHostScaleMin = 1.0;
    private const double VideoHostScaleMax = 8.0;
    private const double VideoHostWheelFactor = 1.18;
    private const int VideoPanKeepAliveThrottleMs = 250;


    public MainWindow() : this(new SettingsService().Load())
    {
    }

    internal MainWindow(AppSettings startupSettings)
    {
        InitializeComponent();
        EnsureCustomWindowStyle();
        _vm = new MainViewModel(_mpvProc, startupSettings);
        DataContext = _vm;
        PreservePendingOpenPath(App.StartupArgs, "startup");
        _videoHost.DoubleClicked += () =>
            Dispatcher.BeginInvoke(() => ExecutePlayerDoubleClick("video-host"));
        _videoHost.ActivationRequested += () => Dispatcher.BeginInvoke(() =>
        {
            Activate();
            try { Root.Focus(); Keyboard.Focus(Root); } catch { }
        }, DispatcherPriority.Input);
        _videoHost.MouseWheelDelta += delta =>
            HandleVideoSurfaceWheel(delta, "video-host");
        _videoHost.MiddleButtonDown += () =>
            BeginVideoPanDrag(GetCurrentCursorScreenPoint(), "video-host");
        _videoHost.MiddleButtonMove += () =>
            QueueVideoPanDrag(GetCurrentCursorScreenPoint(), "video-host");
        _videoHost.MiddleButtonUp += () =>
            EndVideoPanDrag("video-host");

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
        UpdateMaxRestoreButton();
        Topmost = s.AlwaysOnTop;
        _vm.IsAlwaysOnTop = s.AlwaysOnTop;

        VideoHostSlot.Content = _videoHost;


        _mpvProc.Crashed += () => Dispatcher.BeginInvoke(() =>
        {
            if (_closing || _mpvStartupOrRecovery ||
                Volatile.Read(ref _backendOperationInFlight) != 0) return;
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
                _vm.SetLocalizedFailure(
                    PlayerFailureKind.Backend,
                    "MpvProcessRepeatedExit");
            }
        });

        // fullscreen chrome와 native video 입력은 GetCursorPos polling tick에서 처리.
        _vm.Toast       += msg => Dispatcher.BeginInvoke(() => ShowToast(msg));
        _vm.UpdatePromptRequested += req => Dispatcher.BeginInvoke(() => ShowUpdatePrompt(req));
        _vm.RecentToggleRequested += () => Dispatcher.BeginInvoke(ToggleRecentPanel);
        _vm.PlaylistToggleRequested += () => Dispatcher.BeginInvoke(TogglePlaylistPanel);

        SourceInitialized += OnSourceInit;
        Loaded   += OnWindowLoaded;
        Closing += OnWindowClosing;
        DragEnter += OnDragOver;
        DragOver  += OnDragOver;
        DragLeave += OnDragLeave;
        Drop      += OnDrop;
        KeyDown   += OnAnyKey;
        KeyDown   += OnSpaceDown;
        PreviewKeyDown += OnFullscreenShortcutKeyDown;
        PreviewKeyUp += OnSpaceUp;
        StateChanged += OnStateChanged;
        LocationChanged += (_, _) => SyncOverlayWindowPositionsSoon();
        SizeChanged += (_, _) =>
        {
            SyncOverlayWindowPositionsSoon();
            _videoHost.FitVideoViewportToHost();
            ClampNativeVideoPanTarget();
            _videoPanXCurrent = _videoPanXTarget;
            _videoPanYCurrent = _videoPanYTarget;
            ApplyNativeVideoTransform();
        };

        // main window가 deactivate(focus 다른 곳)되면 Space hold timer가 계속 동작해
        // 사용자 의도와 무관하게 2배속 trigger 가능 → timer 중단 + 이미 hold면 복원.
        Deactivated += (_, _) =>
        {
            _spaceHoldTimer?.Stop();
            if (_spaceHeld) { _vm.Speed = _speedBeforeHold; _spaceHeld = false; }
            SyncAmbientVisualAnimations();
        };
        Activated += (_, _) => SyncAmbientVisualAnimations();

        if (Application.Current is { } application)
        {
            application.Activated += OnApplicationActivated;
            application.Deactivated += OnApplicationDeactivated;
        }

        _vm.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(MainViewModel.IsFullscreen):
                    ApplyFullscreen(_vm.IsFullscreen);
                    UpdateMaxRestoreButton();
                    break;
                case nameof(MainViewModel.IsAlwaysOnTop):
                    UpdateEffectiveTopmost();
                    break;
                case nameof(MainViewModel.CurrentMedia):
                    ResetNativeVideoTransform();
                    // 재생 항목으로 자동 스크롤은 PlaylistWindow가 ShowSlide 시 직접 처리.
                    break;
                case nameof(MainViewModel.VideoDisplayAspectRatio):
                    ClampNativeVideoPanTarget();
                    _videoPanXCurrent = _videoPanXTarget;
                    _videoPanYCurrent = _videoPanYTarget;
                    ApplyNativeVideoTransform();
                    break;
                case nameof(MainViewModel.IsBottomBarVisible):
                    ApplyChromeVisibilityForCurrentState();
                    break;
                case nameof(MainViewModel.IsAudioPlayback):
                case nameof(MainViewModel.IsLoading):
                    SyncAmbientVisualAnimations();
                    break;
                case nameof(MainViewModel.IsTrimMode):
                case nameof(MainViewModel.TrimInSec):
                case nameof(MainViewModel.TrimOutSec):
                case nameof(MainViewModel.Duration):
                    Dispatcher.BeginInvoke(new Action(UpdateTrimOverlay),
                        System.Windows.Threading.DispatcherPriority.Render);
                    break;
                case nameof(MainViewModel.TimePos):
                    if (_vm.IsTrimMode)
                        Dispatcher.BeginInvoke(new Action(UpdatePlaybackCursor),
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
    private void OnApplicationActivated(object? sender, EventArgs e)
    {
        if (!_closing)
            UpdateEffectiveTopmost();
    }

    private void OnApplicationDeactivated(object? sender, EventArgs e)
    {
        // foreground 전환이 정착된 뒤 같은 Deno process인지 판정한다.
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!_closing)
                UpdateEffectiveTopmost();
        }), DispatcherPriority.Input);
    }

    private async void OnSourceInit(object? sender, EventArgs e)
    {
        VideoHostSlot.UpdateLayout();
        StartPointerPolling();
        InstallMouseWheelHook();

        // SourceInitialized는 첫 프레임보다 먼저 발생한다. 정상 runtime 확인/시작을
        // Background 우선순위로 넘겨 shell이 먼저 그려지게 한다.
        await Dispatcher.Yield(DispatcherPriority.Background);
        if (_closing) return;

        await InitializePlaybackBackendAsync();
        // native video pointer polling은 첫 실행 준비 화면에서도 동작하도록 SourceInitialized에서 시작.
    }

    private async Task<bool> InitializePlaybackBackendAsync(MediaItem? mediaToResume = null)
    {
        if (_closing ||
            Interlocked.CompareExchange(ref _backendOperationInFlight, 1, 0) != 0)
            return false;

        try
        {
            return await InitializePlaybackBackendCoreAsync(mediaToResume);
        }
        finally
        {
            Volatile.Write(ref _backendOperationInFlight, 0);
        }
    }

    private async Task<bool> InitializePlaybackBackendCoreAsync(MediaItem? mediaToResume)
    {
        if (_closing || _mpvStartupOrRecovery) return false;

        // FFmpeg는 trim/export 시에만 필요하다. 시작 경로에서는 mpv cache만 확인한다.
        RuntimeDependencyService.PreserveExistingMpvCache();

        if (!_mpvProc.MpvAvailable)
        {
            _vm.State = PlayerState.Loading;
            _vm.SetLocalizedStatus("FirstRunStageChecking");

            var prepared = await RuntimeDependencyService.EnsureMpvAsync(
                UpdateFirstRunPrepareStatus,
                _runtimePrepareCts.Token);
            if (_closing) return false;
            if (!prepared.Success || !_mpvProc.MpvAvailable)
            {
                _vm.SetLocalizedFailure(
                    PlayerFailureKind.Backend,
                    "FirstRunPrepareFailed",
                    prepared.ErrorDetail ?? prepared.Error ?? "");
                return false;
            }
        }

        if (_videoHost.Hwnd == IntPtr.Zero)
        {
            _vm.SetLocalizedFailure(PlayerFailureKind.Backend, "VideoHostFailed");
            return false;
        }
        ResetNativeVideoTransform();

        _mpvStartupOrRecovery = true;
        try
        {
            if (_vm.State == PlayerState.Loading && _vm.CurrentMedia is null)
                _vm.SetLocalizedStatus("FirstRunStageStarting");
            try
            {
                await StartMpvAndConnectAsync();
            }
            catch (Exception startFailure)
            {
                if (!await TryRecoverMpvRuntimeAsync(startFailure))
                    throw;
            }
        }
        catch (Exception ex)
        {
            if (_closing) return false;
            _vm.SetLocalizedFailure(
                PlayerFailureKind.Backend,
                "MpvStartFailed",
                FailureDetail(ex));
            return false;
        }
        finally
        {
            _mpvStartupOrRecovery = false;
        }

        if (_closing) return false;

        // backend가 준비되는 동안 들어온 최신 파일 요청이 이전 media resume보다 우선한다.
        var openedPendingPath = ConsumePendingOpenPath();
        if (!openedPendingPath && mediaToResume is not null)
        {
            _vm.State = PlayerState.Loading;
            _vm.PlayMedia(mediaToResume);
        }
        else if (!openedPendingPath &&
                 _vm.State == PlayerState.Loading &&
                 _vm.CurrentMedia is null)
        {
            _vm.StatusMessage = "";
            _vm.State = PlayerState.NoFile;
        }

        return true;
    }

    private async Task StartMpvAndConnectAsync()
    {
        _mpvProc.Start(_videoHost.Hwnd);
        if (!await _vm.ConnectIpcAsync())
            throw new LocalizedDetailException(
                new LocalizedText("MpvIpcFailed"));
    }

    private async Task<bool> TryRecoverMpvRuntimeAsync(Exception startFailure)
    {
        _mpvProc.Dispose();
        if (_closing) return false;

        var mpvPath = _mpvProc.MpvPath;
        var usable = await Task.Run(
            () => RuntimeExecutableValidator.IsUsable(mpvPath, "--version"),
            _runtimePrepareCts.Token);
        if (_closing) return false;

        // 실행 파일 자체가 정상이면 AV/cold-start/일시적인 IPC 초기화 지연일 수 있다.
        // 다운로드나 cache 교체 없이 현재 세션에서 딱 한 번 새 process로 재시도한다.
        if (usable)
        {
            Services.AppLog.Warn($"mpv fast start failed; retrying once with the verified runtime: {startFailure.Message}");
            _vm.State = PlayerState.Loading;
            _vm.SetLocalizedStatus("FirstRunStageStarting");
            await StartMpvAndConnectAsync();
            return true;
        }

        Services.AppLog.Warn($"mpv fast start failed; one verified recovery will run: {startFailure.Message}");
        RuntimeExecutableValidator.Invalidate(mpvPath);
        _vm.State = PlayerState.Loading;
        _vm.SetLocalizedStatus("FirstRunStageChecking");

        var prepared = await RuntimeDependencyService.EnsureMpvAsync(
            UpdateFirstRunPrepareStatus,
            _runtimePrepareCts.Token);
        if (_closing) return false;
        if (!prepared.Success || !_mpvProc.MpvAvailable)
            throw new LocalizedDetailException(
                new LocalizedText(
                    "FirstRunPrepareFailed",
                    prepared.ErrorDetail ?? prepared.Error ?? ""));

        _vm.SetLocalizedStatus("FirstRunStageStarting");
        await StartMpvAndConnectAsync();
        return true;
    }

    private void UpdateFirstRunPrepareStatus(string line)
    {
        var statusKey = FirstRunStatusFromFetcherLine(line);
        if (string.IsNullOrWhiteSpace(statusKey)) return;

        Dispatcher.BeginInvoke(() =>
        {
            if (_closing) return;
            if (_vm.State == PlayerState.Loading && _vm.CurrentMedia is null)
                _vm.SetLocalizedStatus(statusKey);
        });
    }

    private static string? FirstRunStatusFromFetcherLine(string line)
    {
        var lower = line.ToLowerInvariant();
        if (lower.Contains("resolving latest"))
            return "FirstRunStageChecking";
        if (lower.Contains("downloading"))
            return "FirstRunStageDownloading";
        if (lower.Contains("extracting"))
            return "FirstRunStageInstalling";
        if (lower.Contains("already at") || lower.Contains("done"))
            return "FirstRunStageReady";
        return null;
    }

    private async Task RestartMpvAsync()
    {
        if (_closing ||
            Interlocked.CompareExchange(ref _backendOperationInFlight, 1, 0) != 0)
            return;

        _mpvStartupOrRecovery = true;
        try
        {
            _mpvProc.Dispose();
            await Task.Delay(200);
            if (_closing) return;
            _mpvProc.Start(_videoHost.Hwnd);
            if (!await _vm.ConnectIpcAsync())
                throw new LocalizedDetailException(
                    new LocalizedText("MpvIpcFailed"));
            ResetNativeVideoTransform();
            _vm.StatusMessage = "";
            // 성공한 backend recovery가 이전 Failed/Backend state를 남기지 않게
            // 먼저 정상 state로 전환한 뒤, 최신 외부 요청 또는 이전 media를 다시 연다.
            _vm.State = PlayerState.Loading;
            var openedPendingPath = ConsumePendingOpenPath();
            if (!openedPendingPath && _vm.CurrentMedia is { } cm)
            {
                _vm.PlayMedia(cm);
            }
            else if (!openedPendingPath)
            {
                _vm.State = PlayerState.NoFile;
            }
        }
        catch (Exception ex)
        {
            _vm.SetLocalizedFailure(
                PlayerFailureKind.Backend,
                "MpvRestartFailed",
                FailureDetail(ex));
        }
        finally
        {
            _mpvStartupOrRecovery = false;
            Volatile.Write(ref _backendOperationInFlight, 0);
        }
    }

    private static string? FirstValidFilePath(IEnumerable<string> args)
    {
        foreach (var path in args)
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                return path;
        }

        return null;
    }

    private static object FailureDetail(Exception exception) =>
        exception is LocalizedDetailException localized
            ? localized.Detail
            : exception.Message;

    private bool PreservePendingOpenPath(IEnumerable<string> args, string source)
    {
        var path = FirstValidFilePath(args);
        if (path is null) return false;

        _pendingOpenPath = path;
        Services.AppLog.Info($"PendingOpen[{source}] preserved '{path}'");
        return true;
    }

    private bool ConsumePendingOpenPath()
    {
        var path = _pendingOpenPath;
        if (path is null) return false;

        _pendingOpenPath = null;
        Services.AppLog.Info($"PendingOpen consumed '{path}'");
        _vm.OpenPath(path);
        return true;
    }

    /// <summary>다른 인스턴스가 인자를 보냄 (single-instance hand-off).</summary>
    public void ReceiveExternalArgs(string[] args)
    {
        Services.AppLog.Info($"ReceiveExternalArgs entry n={args?.Length ?? -1} access={Dispatcher.CheckAccess()}");
        if (args is null) return;
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
        Topmost = true;
        UpdateEffectiveTopmost();
        var path = FirstValidFilePath(args);
        if (path is null)
        {
            if (args.Length > 0)
                Services.AppLog.Warn("ApplyExternalArgs: no valid file path");
            return;
        }

        if (PlayerFailurePolicy.CanStartMediaPlayback(
                _vm.State,
                _vm.FailureKind,
                _vm.IsBackendConnected) &&
            !_mpvStartupOrRecovery &&
            Volatile.Read(ref _backendOperationInFlight) == 0)
        {
            _vm.OpenPath(path);
            return;
        }

        PreservePendingOpenPath(new[] { path }, "external");
    }

    // ============================================================
    // 닫기
    // ============================================================
    private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _closing = true;
        if (Application.Current is { } application)
        {
            application.Activated -= OnApplicationActivated;
            application.Deactivated -= OnApplicationDeactivated;
        }
        StopAmbientVisualAnimations();
        try { _runtimePrepareCts.Cancel(); } catch { }
        try { _pointerPoll?.Stop(); } catch { }
        UninstallMouseWheelHook();
        _pointerPoll = null;
        try { _playlistWin?.Close(); } catch { }
        try { _recentWin?.Close(); } catch { }
        try { _toastWin?.Close(); } catch { }
        try { _helpWin?.Close(); } catch { }
        _playlistWin = null;
        _recentWin = null;
        _toastWin = null;
        _helpWin = null;
        double? l;
        double? t;
        double w;
        double h;
        var maximized = WindowState == WindowState.Maximized;
        if (_vm.IsFullscreen && _hasFullscreenRestoreBounds)
        {
            maximized = _savedWasMaximized;
            l = maximized ? null : _savedLeft;
            t = maximized ? null : _savedTop;
            w = _savedWidth;
            h = _savedHeight;
        }
        else
        {
            l = WindowState == WindowState.Normal ? Left : null;
            t = WindowState == WindowState.Normal ? Top : null;
            w = WindowState == WindowState.Normal ? Width : RestoreBounds.Width;
            h = WindowState == WindowState.Normal ? Height : RestoreBounds.Height;
        }
        _vm.PersistSettings(w, h, l, t, maximized);
        _vm.Dispose();
        _mpvProc.Dispose();
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        UpdateMaxRestoreButton();
        SyncAmbientVisualAnimations();
        SyncPlaylistWindowPosition();
        SyncRecentWindowPosition();
        // 최소화될 때 playlist도 같이 숨김 (Owner가 minimize면 자동이긴 하지만 명시)
        if (WindowState == WindowState.Minimized)
        {
            DismissPlaylistPanel();
            DismissRecentPanel();
        }
    }

    // ============================================================
    // TopBar 빈 영역 드래그 + 표준 최대화/복원
    // ============================================================
    // 타이틀바 드래그 (단일 click + hold → drag). 더블클릭은 Windows 관례대로
    // 일반 창과 최대화 상태를 전환한다.
    // deferred-drag 패턴: MouseDown에서 arm만, MouseMove threshold 넘으면 DragMove.
    // 빠른 더블클릭은 mouse 안 움직여서 DragMove modal loop 발생 X → 두 번째 click 정상 fire.
    private bool _topBarDragArmed;
    private Point _topBarDownPos;

    private void OnTopBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        // 버튼 click은 우리 처리 X
        if (e.OriginalSource is DependencyObject d && FindAncestor<Button>(d) is not null) return;
        if (e.ClickCount >= 2)
        {
            _topBarDragArmed = false;
            ToggleWindowMaximizeRestore("topbar");
            e.Handled = true;
            return;
        }
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
    private Visibility HiddenChromeVisibility => _vm.IsFullscreen ? Visibility.Hidden : Visibility.Collapsed;

    private void OnRootMouseMove(object sender, MouseEventArgs e)
    {
        if (_videoPanDragging)
        {
            // Remapped/synthetic middle input can disagree with GetAsyncKeyState.
            // A matching explicit middle-up ends the gesture; movement never does.
            QueueVideoPanDrag(GetCurrentCursorScreenPoint(), "root-move");
            return;
        }

        if (ShouldIgnoreStationaryFullscreenReveal(GetCurrentCursorScreenPoint()))
            return;
        ShowControls("root-move");
    }

    private void OnRootPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        _ignoreStationaryFullscreenReveal = false;
        ShowControls("mouse-down");
        // 영상 위(=mpv child hwnd) click 시 OS가 mpv hwnd에 keyboard focus를 줘서
        // WPF KeyBinding이 fire 안 됨 (F/V/Ctrl+S 등 단축키 죽음).
        // click마다 Root grid(Focusable=True)에 focus 강제 — WPF visual tree 안 element가
        // focus 가지면 Window.InputBindings가 keyboard input 받음.
        try { Root.Focus(); Keyboard.Focus(Root); } catch { }

        if (e.ChangedButton == MouseButton.Middle && IsVideoZoomPointerTarget(e.OriginalSource as DependencyObject))
        {
            BeginVideoPanDrag(GetCurrentCursorScreenPoint(), "middle-click");
            e.Handled = true;
        }
    }

    private void ShowFullscreenControlsOnEntry()
    {
        _ignoreStationaryFullscreenReveal = false;
        ShowControls("fullscreen-entry");
    }

    // 현재 OSD 표시 상태 — animation 재트리거 방지 (mpv mouse-pos가 픽셀당 보고)
    private bool _osdShown = true;

    private void ShowControls(string source = "unknown")
    {
        if (_vm.IsFullscreen && (!_osdShown || _ignoreStationaryFullscreenReveal))
            Services.AppLog.Info($"ShowControls[{source}] osd={_osdShown} guard={_ignoreStationaryFullscreenReveal}");
        _ignoreStationaryFullscreenReveal = false;
        if (_vm.IsFullscreen)
            UpdateMediaLayerForFullscreen(immersive: false);

        var alreadyShown = _osdShown &&
            TopBar.Visibility == Visibility.Visible &&
            TopBar.Opacity >= 0.99 &&
            (!_vm.IsBottomBarVisible ||
             (BottomBar.Visibility == Visibility.Visible && BottomBar.Opacity >= 0.99));
        if (alreadyShown)
        {
            Mouse.OverrideCursor = null;
            return;
        }

        _osdShown = true;
        SetChromeVisibility(visible: true);
        FadeTo(TopBar, 1.0, 120);
        if (_vm.IsBottomBarVisible)
            FadeTo(BottomBar, 1.0, 120);
        Mouse.OverrideCursor = null;
        SyncOverlayWindowPositionsSoon();
    }

    private void HideControls(bool force = false, string source = "unknown")
    {
        if (_ownedModalOpen)
        {
            Mouse.OverrideCursor = null;
            return;
        }
        if (ControlsAlwaysOn)
        {
            if (force) Services.AppLog.Info($"HideControls[{source}] skipped: windowed");
            return;
        }
        if (!force && (TopBar.IsMouseOver || BottomBar.IsMouseOver))
        {
            return;
        }
        if (force && Mouse.LeftButton != MouseButtonState.Pressed)
        {
            if (_seekDragging) _vm.EndSeek(SeekBar.Value);
            _seekDragging = false;
            _volDragging = false;
            try { SeekBar.ReleaseMouseCapture(); } catch { }
            try { VolumeBar.ReleaseMouseCapture(); } catch { }
        }

        if (_seekDragging || _volDragging || (!force && _vm.Seeking))
        {
            Services.AppLog.Info(
                $"HideControls[{source}] blocked force={force} seeking={_vm.Seeking} seekDrag={_seekDragging} volDrag={_volDragging}");
            return;
        }

        var visible = TopBar.Visibility == Visibility.Visible || BottomBar.Visibility == Visibility.Visible;
        if (!_osdShown && !visible) return;

        _osdShown = false;
        if (_vm.IsFullscreen)
            ArmStationaryFullscreenRevealGuard();

        Services.AppLog.Info(
            $"HideControls[{source}] applied force={force} top={TopBar.Visibility} bottom={BottomBar.Visibility}");

        if (force)
            HideControlsNow();
        else
        {
            var pending = _vm.IsBottomBarVisible ? 2 : 1;
            void OnChromeHidden()
            {
                pending--;
                if (pending <= 0 && _vm.IsFullscreen && !_osdShown)
                    UpdateMediaLayerForFullscreen(immersive: true);
            }

            FadeTo(TopBar, 0.0, 200, hideAfter: true,
                hideVisibility: HiddenChromeVisibility, onCompleted: OnChromeHidden);
            if (_vm.IsBottomBarVisible)
                FadeTo(BottomBar, 0.0, 200, hideAfter: true,
                    hideVisibility: HiddenChromeVisibility, onCompleted: OnChromeHidden);
            else
                BottomBar.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Collapsed);
        }

        if (_vm.IsFullscreen)
            Mouse.OverrideCursor = Cursors.None;
        SyncOverlayWindowPositionsSoon();
    }

    private void HideControlsNow()
    {
        TopBar.BeginAnimation(UIElement.OpacityProperty, null);
        BottomBar.BeginAnimation(UIElement.OpacityProperty, null);
        TopBar.Opacity = 0.0;
        BottomBar.Opacity = 0.0;
        SetChromeVisibility(visible: false);
        if (_vm.IsFullscreen)
            UpdateMediaLayerForFullscreen(immersive: true);
    }

    private void SetChromeVisibility(bool visible)
    {
        var topVisibility = visible ? Visibility.Visible : HiddenChromeVisibility;
        var bottomVisibility = !_vm.IsBottomBarVisible
            ? Visibility.Collapsed
            : visible ? Visibility.Visible : HiddenChromeVisibility;
        TopBar.SetCurrentValue(UIElement.VisibilityProperty, topVisibility);
        BottomBar.SetCurrentValue(UIElement.VisibilityProperty, bottomVisibility);
    }

    private void ApplyChromeVisibilityForCurrentState()
    {
        if (ControlsAlwaysOn || _osdShown)
            SetChromeVisibility(visible: true);
        else
            SetChromeVisibility(visible: false);
    }

    private void ArmStationaryFullscreenRevealGuard()
    {
        if (!_vm.IsFullscreen) return;
        _fullscreenHiddenAtMouse = GetCurrentCursorScreenPoint();
        _lastPollMouse = _fullscreenHiddenAtMouse;
        _ignoreStationaryFullscreenReveal = true;
    }

    private Point GetCurrentCursorScreenPoint() =>
        GetCursorPos(out var pt) ? new Point(pt.X, pt.Y) : _lastPollMouse;

    private bool ShouldIgnoreStationaryFullscreenReveal(Point screenPoint)
    {
        if (!_ignoreStationaryFullscreenReveal || !_vm.IsFullscreen || _osdShown)
            return false;

        var dx = Math.Abs(screenPoint.X - _fullscreenHiddenAtMouse.X);
        var dy = Math.Abs(screenPoint.Y - _fullscreenHiddenAtMouse.Y);
        if (dx <= FullscreenStationaryRevealThreshold && dy <= FullscreenStationaryRevealThreshold)
            return true;

        _ignoreStationaryFullscreenReveal = false;
        return false;
    }

    private void BeginOwnedModalInteraction(string source)
    {
        _ownedModalOpen = true;
        _ignoreStationaryFullscreenReveal = false;
        Mouse.OverrideCursor = null;
        DismissPlaylistPanel();
        DismissRecentPanel();
        if (_vm.IsFullscreen)
        {
            Services.AppLog.Info($"OwnedModal[{source}] begin");
            _osdShown = true;
            TopBar.BeginAnimation(UIElement.OpacityProperty, null);
            BottomBar.BeginAnimation(UIElement.OpacityProperty, null);
            TopBar.Opacity = 1.0;
            BottomBar.Opacity = 1.0;
            SetChromeVisibility(visible: true);
            UpdateMediaLayerForFullscreen(immersive: false);
            SyncOverlayWindowPositionsSoon();
        }
    }

    private void EndOwnedModalInteraction(string source)
    {
        _ownedModalOpen = false;
        _ignoreStationaryFullscreenReveal = false;
        Mouse.OverrideCursor = null;
        if (_vm.IsFullscreen && !_closing)
        {
            Services.AppLog.Info($"OwnedModal[{source}] end");
        }
    }

    // ============================================================
    // Toast — owned ToastWindow로 위임 (WPF airspace 우회)
    // ============================================================
    private async void ShowUpdatePrompt(UpdatePromptRequest request)
    {
        if (_closing || _updatePromptOpen) return;
        _updatePromptOpen = true;

        try
        {
            BeginOwnedModalInteraction("update");
            if (WindowState == WindowState.Minimized)
                WindowState = WindowState.Normal;

            Activate();

            var messageKey = request.Portable
                ? "UpdatePromptPortableMessage"
                : request.ReadyToApply
                    ? "UpdatePromptReadyMessage"
                    : "UpdatePromptAvailableMessage";

            var primaryKey = request.Portable
                ? "UpdatePromptDownloadButton"
                : "UpdatePromptUpdateButton";

            var dlg = new UpdatePromptWindow(
                LocalizationService.T("UpdatePromptTitle"),
                LocalizationService.F(messageKey, request.NewVersion),
                LocalizationService.T(primaryKey),
                LocalizationService.T("UpdatePromptCancelButton"))
            {
                Owner = this
            };

            var accepted = dlg.ShowDialog() == true;
            EndOwnedModalInteraction("update");
            if (accepted)
                await _vm.ApplyPendingUpdateAsync().ConfigureAwait(true);
            else
                _vm.DismissPendingUpdateForCurrentRun();
        }
        catch (Exception ex)
        {
            Services.AppLog.Error("ShowUpdatePrompt", ex);
            ShowToast(LocalizationService.T("UpdateApplyFailed"));
        }
        finally
        {
            if (_ownedModalOpen)
                EndOwnedModalInteraction("update-finally");
            _updatePromptOpen = false;
        }
    }

    private void ShowToast(string message)
    {
        if (_toastWin is null)
        {
            try { _toastWin = new ToastWindow { Owner = this }; }
            catch { return; }
        }
        _toastWin.Show(message);
    }

    private static void FadeTo(UIElement el, double target, int ms, bool hideAfter = false,
        Visibility hideVisibility = Visibility.Collapsed, Action? onCompleted = null)
    {
        var anim = new DoubleAnimation
        {
            To = target,
            Duration = TimeSpan.FromMilliseconds(ms),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        if (hideAfter || onCompleted is not null)
            anim.Completed += (_, _) =>
            {
                if (hideAfter && Math.Abs(target) < 0.01)
                    el.SetCurrentValue(UIElement.VisibilityProperty, hideVisibility);
                onCompleted?.Invoke();
            };
        el.BeginAnimation(UIElement.OpacityProperty, anim);
    }

    // ============================================================
    // Recent / playlist owned windows — 명시적 toggle slide panels
    // ============================================================
    private void OnWindowLoaded(object? sender, RoutedEventArgs e)
    {
        SyncAmbientVisualAnimations();
        // 별도 panel XAML/HWND 준비가 첫 main-window render를 막지 않게 idle에서 예열한다.
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!_closing) EnsurePanelWindows();
        }), DispatcherPriority.ContextIdle);
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!_closing) _ = _vm.PruneMissingRecentsAsync(save: true);
        }),
            DispatcherPriority.ApplicationIdle);
    }

    private void EnsurePanelWindows()
    {
        if (_closing || !IsLoaded) return;
        if (_playlistWin is null)
        {
            _playlistWin = new PlaylistWindow { Owner = this };
            _playlistWin.DataContext = _vm;
            _playlistWin.ShownChanged += OnPlaylistShownChanged;
        }
        if (_recentWin is null)
        {
            _recentWin = new RecentWindow { Owner = this };
            _recentWin.DataContext = _vm;
            _recentWin.ShownChanged += OnRecentShownChanged;
        }
        // _toastWin은 lazy create — 첫 ShowToast 시점에 생성. 항상 떠있으면 mouse-pos
        // routing이나 다른 owned window들 layout에 영향 가능.
        SyncPlaylistWindowPosition();
        SyncRecentWindowPosition();
    }

    private void ShowPlaylistPanel()
    {
        DismissRecentPanel();
        EnsurePanelWindows();
        if (_playlistWin is null) return;
        SyncPlaylistWindowPosition();
        _playlistWin.Topmost = Topmost;
        // 입력 직후 동기 파일 write가 animation 첫 frame을 막지 않게 기본 비활성 진단으로 둔다.
        Services.AppLog.Debug($"Panel[right] show fs={_vm.IsFullscreen} osd={_osdShown}");
        _playlistWin.ShowSlide();
    }

    private void TogglePlaylistPanel()
    {
        if (_playlistWin?.IsShown == true)
            DismissPlaylistPanel();
        else
            ShowPlaylistPanel();
    }

    private void DismissPlaylistPanel()
    {
        _playlistWin?.HideSlide();
    }

    private void ShowRecentPanel()
    {
        DismissPlaylistPanel();
        EnsurePanelWindows();
        if (_recentWin is null) return;
        SyncRecentWindowPosition();
        _recentWin.Topmost = Topmost;
        Services.AppLog.Debug($"Panel[left] show fs={_vm.IsFullscreen} osd={_osdShown}");
        _recentWin.ShowSlide();
    }

    private void ToggleRecentPanel()
    {
        if (_recentWin?.IsShown == true)
            DismissRecentPanel();
        else
            ShowRecentPanel();
    }

    private void DismissRecentPanel()
    {
        _recentWin?.HideSlide();
    }

    private void SyncOverlayWindowPositionsSoon()
    {
        if (Interlocked.Exchange(ref _overlaySyncScheduled, 1) != 0) return;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            Interlocked.Exchange(ref _overlaySyncScheduled, 0);
            if (_closing) return;
            SyncPlaylistWindowPosition();
            SyncRecentWindowPosition();
        }), DispatcherPriority.Render);
    }

    private double PanelTopOffset => _vm.IsFullscreen && !_osdShown
        ? 0
        : (TopBar?.ActualHeight > 0 ? TopBar.ActualHeight : 36);

    private double PanelBottomReserved => _vm.IsFullscreen && !_osdShown
        ? 0
        : (BottomBar?.ActualHeight > 0 ? BottomBar.ActualHeight : 80) + 12;

    private void SyncRecentWindowPosition()
    {
        if (_recentWin is null || WindowState == WindowState.Minimized) return;
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero || !GetWindowRect(hwnd, out var r)) return;
        if (WindowState == WindowState.Maximized && !_vm.IsFullscreen &&
            TryGetMonitorWorkArea(hwnd, out var workArea))
            ClampWindowRectToWorkArea(ref r, workArea);

        var dpi = VisualTreeHelper.GetDpi(this);
        var sx = dpi.DpiScaleX <= 0 ? 1.0 : dpi.DpiScaleX;
        var sy = dpi.DpiScaleY <= 0 ? 1.0 : dpi.DpiScaleY;
        var left = r.Left / sx;
        var top = r.Top / sy;
        var width = (r.Right - r.Left) / sx;
        var height = (r.Bottom - r.Top) / sy;
        if (width <= 0 || height <= 0) return;

        var topOffset = PanelTopOffset;
        var bottomReserved = PanelBottomReserved;
        _recentWin.Left = left;
        _recentWin.Top = top + topOffset;
        _recentWin.Height = Math.Max(160, height - topOffset - bottomReserved);
    }

    /// <summary>
    /// 재생목록 panel은 영상 위에 그냥 덮음(사용자 요청). 영상 host 크기는 건드리지 않아서
    /// mpv가 매번 letterbox 재계산할 일이 없음 — 영상 비율/위치 그대로 유지.
    /// 이벤트는 남겨두지만 더 이상 영상 host margin을 만지지 않는다.
    /// </summary>
    private void OnPlaylistShownChanged(bool shown) => _vm.SetPlaylistOpen(shown);
    private void OnRecentShownChanged(bool shown) => _vm.SetRecentOpen(shown);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hwnd, out PInvokeRect rect);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = CharSet.Auto)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    private const uint MonitorDefaultToNearest = 2;

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct PInvokeRect { public int Left, Top, Right, Bottom; }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public PInvokeRect Monitor;
        public PInvokeRect WorkArea;
        public uint Flags;
    }

    private static bool TryGetMonitorWorkArea(IntPtr hwnd, out PInvokeRect workArea)
    {
        workArea = default;
        var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero) return false;
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref info)) return false;
        workArea = info.WorkArea;
        return true;
    }

    private static void ClampWindowRectToWorkArea(ref PInvokeRect rect, PInvokeRect workArea)
    {
        rect.Left = Math.Max(rect.Left, workArea.Left);
        rect.Top = Math.Max(rect.Top, workArea.Top);
        rect.Right = Math.Min(rect.Right, workArea.Right);
        rect.Bottom = Math.Min(rect.Bottom, workArea.Bottom);
    }

    private void SyncPlaylistWindowPosition()
    {
        if (_playlistWin is null || WindowState == WindowState.Minimized) return;

        // 풀스크린/Maximized/멀티모니터/HiDPI에서 WPF Left/ActualWidth가 부정확할 수 있음.
        // hwnd의 실제 화면 좌표를 직접 가져온 뒤 DPI scale로 DIP 변환. 이 값은 어떤 환경에서도 신뢰 가능.
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero || !GetWindowRect(hwnd, out var r)) return;
        if (WindowState == WindowState.Maximized && !_vm.IsFullscreen &&
            TryGetMonitorWorkArea(hwnd, out var workArea))
            ClampWindowRectToWorkArea(ref r, workArea);

        var dpi = VisualTreeHelper.GetDpi(this);
        var sx = dpi.DpiScaleX <= 0 ? 1.0 : dpi.DpiScaleX;
        var sy = dpi.DpiScaleY <= 0 ? 1.0 : dpi.DpiScaleY;
        var left = r.Left / sx;
        var top = r.Top / sy;
        var width = (r.Right - r.Left) / sx;
        var height = (r.Bottom - r.Top) / sy;
        if (width <= 0 || height <= 0) return;

        var topOffset = PanelTopOffset;
        var bottomReserved = PanelBottomReserved;
        _playlistWin.Left = left + width - _playlistWin.Width;
        _playlistWin.Top = top + topOffset;
        _playlistWin.Height = Math.Max(160, height - topOffset - bottomReserved);
    }

    // ============================================================
    // GetCursorPos polling — mpv IPC mouse-pos는 좌표계 신뢰 어려움(host hwnd 안
    // native pixel/logical/video pixel 어떤지 환경마다 다름) + WPF MouseMove는 HwndHost
    // 위에서 fire 안 됨. Win32 GetCursorPos로 직접 screen coord 받아 fullscreen chrome,
    // double-click, pan 상태를 짧게 polling한다.
    // ============================================================
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out PinvokePoint lpPoint);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(PinvokePoint point);
    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);
    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool IsChild(IntPtr hWndParent, IntPtr hWnd);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDoubleClickTime();
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);
    private const int SmCxDoubleClick = 36;
    private const int SmCyDoubleClick = 37;
    private const int WhMouseLl = 14;
    private const int WmMouseMove = 0x0200;
    private const int WmMButtonDown = 0x0207;
    private const int WmMButtonUp = 0x0208;
    private const int WmMouseWheel = 0x020A;
    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct PinvokePoint { public int X; public int Y; }
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct LowLevelMouseHookStruct
    {
        public PinvokePoint pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    private DispatcherTimer? _pointerPoll;
    private Point _lastPollMouse;

    private void StartPointerPolling()
    {
        if (_pointerPoll is not null) return;
        _pointerPoll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
        _pointerPoll.Tick += OnPointerPollTick;
        _pointerPoll.Start();
    }

    private void InstallMouseWheelHook()
    {
        if (_mouseWheelHook != IntPtr.Zero) return;
        _mouseWheelHookProc = OnLowLevelMouseInput;
        _mouseWheelHook = SetWindowsHookEx(WhMouseLl, _mouseWheelHookProc, GetModuleHandle(null), 0);
        if (_mouseWheelHook == IntPtr.Zero)
            Services.AppLog.Warn("Mouse wheel hook install failed");
    }

    private void UninstallMouseWheelHook()
    {
        if (_mouseWheelHook == IntPtr.Zero) return;
        try { UnhookWindowsHookEx(_mouseWheelHook); } catch { }
        _mouseWheelHook = IntPtr.Zero;
        _mouseWheelHookProc = null;
    }

    private IntPtr OnLowLevelMouseInput(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            try
            {
                var message = wParam.ToInt32();
                // 1000Hz mouse에서는 대부분이 평범한 WM_MOUSEMOVE다. pan 중이 아닐 때는
                // 구조체 marshal/Point 생성 전에 통과시켜 UI thread와 panel animation을 보호한다.
                var needsCoordinates = message == WmMouseWheel ||
                                       message == WmMButtonDown ||
                                       message == WmMButtonUp ||
                                       (message == WmMouseMove && _videoPanDragging);
                if (!needsCoordinates)
                    return CallNextHookEx(_mouseWheelHook, nCode, wParam, lParam);

                var info = Marshal.PtrToStructure<LowLevelMouseHookStruct>(lParam);
                var screenPoint = new Point(info.pt.X, info.pt.Y);
                if (message == WmMouseWheel && ShouldHandleGlobalVideoWheel(screenPoint))
                {
                    var delta = (short)((info.mouseData >> 16) & 0xffff);
                    // Hook callback 시점의 modifier 상태를 보존한다. Dispatcher가 밀린 뒤
                    // Ctrl 상태를 다시 읽으면 빠른 key-up에서 wheel 의미가 volume으로 바뀐다.
                    HandleVideoSurfaceWheel(delta, "mouse-hook", IsVideoZoomWheelModifierDown());
                    return new IntPtr(1);
                }

                // mpv child HWND, capture 경계, remapped middle button을 모두 같은 경로로
                // 추적한다. Hook은 이벤트를 삼키지 않고 앱의 drag state만 갱신한다.
                if (message == WmMButtonDown && ShouldBeginGlobalVideoPan(screenPoint))
                    BeginVideoPanDrag(screenPoint, "mouse-hook");
                else if (message == WmMouseMove && _videoPanDragging)
                    QueueVideoPanDrag(screenPoint, "mouse-hook");
                else if (message == WmMButtonUp && _videoPanDragging)
                    EndVideoPanDrag("mouse-hook");
            }
            catch (Exception ex)
            {
                Services.AppLog.Warn($"Mouse input hook failed: {ex.Message}");
            }
        }

        return CallNextHookEx(_mouseWheelHook, nCode, wParam, lParam);
    }

    private void SyncAmbientVisualAnimations()
    {
        var canAnimate = !_closing && IsVisible && IsActive && WindowState != WindowState.Minimized;
        SetAudioAnimation(canAnimate && _vm.IsAudioPlayback);
        SetLoadingAnimation(canAnimate && _vm.IsLoading);
    }

    private void StopAmbientVisualAnimations()
    {
        SetAudioAnimation(false);
        SetLoadingAnimation(false);
    }

    private void SetAudioAnimation(bool shouldRun)
    {
        if (!shouldRun)
        {
            if (!_audioAnimationRunning && !AudioBounce.HasAnimatedProperties) return;
            _audioAnimationRunning = false;
            AudioBounce.BeginAnimation(TranslateTransform.YProperty, null);
            AudioBounce.Y = 0;
            return;
        }
        if (_audioAnimationRunning) return;
        _audioAnimationRunning = true;

        var bounce = new DoubleAnimation
        {
            From = 0,
            To = -22,
            Duration = TimeSpan.FromMilliseconds(550),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        AudioBounce.BeginAnimation(TranslateTransform.YProperty, bounce,
            HandoffBehavior.SnapshotAndReplace);
    }

    private void SetLoadingAnimation(bool shouldRun)
    {
        if (!shouldRun)
        {
            if (!_loadingAnimationRunning &&
                !LoadingDotScale.HasAnimatedProperties &&
                !LoadingTx.HasAnimatedProperties)
                return;
            _loadingAnimationRunning = false;
            LoadingDotScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            LoadingDotScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            LoadingTx.BeginAnimation(TranslateTransform.XProperty, null);
            LoadingDotScale.ScaleX = 1;
            LoadingDotScale.ScaleY = 1;
            LoadingTx.X = -110;
            return;
        }
        if (_loadingAnimationRunning) return;
        _loadingAnimationRunning = true;

        static DoubleAnimation Pulse() => new()
        {
            From = 0.82,
            To = 1.18,
            Duration = TimeSpan.FromMilliseconds(720),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever
        };

        LoadingDotScale.BeginAnimation(ScaleTransform.ScaleXProperty, Pulse(),
            HandoffBehavior.SnapshotAndReplace);
        LoadingDotScale.BeginAnimation(ScaleTransform.ScaleYProperty, Pulse(),
            HandoffBehavior.SnapshotAndReplace);

        var sweep = new DoubleAnimation
        {
            From = -110,
            To = 320,
            Duration = TimeSpan.FromMilliseconds(1350),
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        LoadingTx.BeginAnimation(TranslateTransform.XProperty, sweep,
            HandoffBehavior.SnapshotAndReplace);
    }

    private bool ShouldHandleGlobalVideoWheel(Point screenPoint)
    {
        if (_closing || _ownedModalOpen || !IsActive)
            return false;
        if (!IsCursorOverNativeVideoSurface(screenPoint))
            return false;
        return true;
    }

    private bool ShouldBeginGlobalVideoPan(Point screenPoint)
    {
        if (_closing || _ownedModalOpen || !IsActive || !_vm.CanZoomVideo)
            return false;
        if (_videoScaleTarget <= VideoHostScaleMin + 0.001)
            return false;
        return IsCursorOverNativeVideoSurface(screenPoint);
    }

    private void OnPointerPollTick(object? sender, EventArgs e)
    {
        if (_closing) return;
        if (_ownedModalOpen)
        {
            Mouse.OverrideCursor = null;
            EndVideoPanDrag("modal");
            return;
        }

        if (!TryGetCursorInRoot(out var screenPoint, out var relX, out var relY, out var w, out var h))
        {
            EndVideoPanDrag("cursor-lost");
            return;
        }
        if (IsCursorOverHelpWindow(screenPoint))
        {
            Mouse.OverrideCursor = null;
            EndVideoPanDrag("help");
            return;
        }

        var cursorInRoot = relX >= 0 && relX < w && relY >= 0 && relY < h;
        var cursorOverThisApp = IsCursorOverThisApp(screenPoint);
        if (!IsActive && !cursorOverThisApp)
        {
            EndVideoPanDrag("inactive");
            return;
        }

        if (PollVideoPanDrag(screenPoint))
            return;

        if (IsActive && IsCursorOverNativeVideoSurface(screenPoint))
            PollFullscreenDoubleClick(relX, relY, w, h);
        else
            _pollLeftButtonWasDown = false;

        // Mouse 움직임 감지 → ShowControls (이전엔 mpv MouseActivity event가 담당).
        // 풀스크린 모드에서 마우스 움직일 때 OSD 다시 보이게 하는 게 핵심.
        if (IsActive &&
            (Math.Abs(screenPoint.X - _lastPollMouse.X) > 0.5 ||
             Math.Abs(screenPoint.Y - _lastPollMouse.Y) > 0.5))
        {
            _lastPollMouse = screenPoint;
            if (cursorInRoot)
            {
                if (ShouldIgnoreStationaryFullscreenReveal(screenPoint))
                    return;
                ShowControls("cursor-poll");
            }
        }

        if (ShouldIgnoreStationaryFullscreenReveal(screenPoint))
            return;
    }

    private bool IsCursorOverThisApp(Point screenPoint)
    {
        var hit = WindowFromPoint(new PinvokePoint
        {
            X = (int)Math.Round(screenPoint.X),
            Y = (int)Math.Round(screenPoint.Y)
        });
        if (hit == IntPtr.Zero) return false;

        var main = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (IsWindowOrChild(main, hit)) return true;

        if (_playlistWin is not null)
        {
            var playlist = new System.Windows.Interop.WindowInteropHelper(_playlistWin).Handle;
            if (IsWindowOrChild(playlist, hit)) return true;
        }

        if (_recentWin is not null)
        {
            var recent = new System.Windows.Interop.WindowInteropHelper(_recentWin).Handle;
            if (IsWindowOrChild(recent, hit)) return true;
        }

        if (GetWindowThreadProcessId(hit, out var processId) == 0) return false;
        if (processId == (uint)Environment.ProcessId) return true;

        try
        {
            var mpv = _mpvProc.Process;
            if (mpv is { HasExited: false } && processId == (uint)mpv.Id)
                return true;
        }
        catch { /* process may exit while polling */ }

        return false;
    }

    private bool IsCursorOverHelpWindow(Point screenPoint)
    {
        if (_helpWin?.IsVisible != true) return false;
        var hit = WindowFromPoint(new PinvokePoint
        {
            X = (int)Math.Round(screenPoint.X),
            Y = (int)Math.Round(screenPoint.Y)
        });
        if (hit == IntPtr.Zero) return false;
        var help = new System.Windows.Interop.WindowInteropHelper(_helpWin).Handle;
        return IsWindowOrChild(help, hit);
    }

    private bool IsCursorOverNativeVideoSurface(Point screenPoint)
    {
        var hit = WindowFromPoint(new PinvokePoint
        {
            X = (int)Math.Round(screenPoint.X),
            Y = (int)Math.Round(screenPoint.Y)
        });
        if (hit == IntPtr.Zero) return false;

        if (_videoHost.Hwnd != IntPtr.Zero && IsWindowOrChild(_videoHost.Hwnd, hit))
            return true;

        try
        {
            var mpv = _mpvProc.Process;
            return mpv is { HasExited: false }
                   && GetWindowThreadProcessId(hit, out var processId) != 0
                   && processId == (uint)mpv.Id;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsWindowOrChild(IntPtr parent, IntPtr hit) =>
        parent != IntPtr.Zero && (hit == parent || IsChild(parent, hit));

    private bool TryGetCursorInRoot(out Point screenPoint, out double relX, out double relY, out double w, out double h)
    {
        screenPoint = default;
        relX = relY = w = h = 0;

        if (!GetCursorPos(out var pt)) return false;
        screenPoint = new Point(pt.X, pt.Y);

        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero || !GetWindowRect(hwnd, out var rect)) return false;

        var src = System.Windows.PresentationSource.FromVisual(this);
        if (src?.CompositionTarget is not null)
        {
            var toDip = src.CompositionTarget.TransformFromDevice;
            var topLeft = toDip.Transform(new Point(rect.Left, rect.Top));
            var bottomRight = toDip.Transform(new Point(rect.Right, rect.Bottom));
            var cursor = toDip.Transform(screenPoint);
            relX = cursor.X - topLeft.X;
            relY = cursor.Y - topLeft.Y;
            w = bottomRight.X - topLeft.X;
            h = bottomRight.Y - topLeft.Y;
        }
        else
        {
            var dpi = VisualTreeHelper.GetDpi(this);
            var sx = dpi.DpiScaleX <= 0 ? 1.0 : dpi.DpiScaleX;
            var sy = dpi.DpiScaleY <= 0 ? 1.0 : dpi.DpiScaleY;
            relX = (pt.X - rect.Left) / sx;
            relY = (pt.Y - rect.Top) / sy;
            w = (rect.Right - rect.Left) / sx;
            h = (rect.Bottom - rect.Top) / sy;
        }

        if (Root.ActualWidth > 0 && Math.Abs(Root.ActualWidth - w) <= 24)
            w = Root.ActualWidth;
        if (Root.ActualHeight > 0 && Math.Abs(Root.ActualHeight - h) <= 24)
            h = Root.ActualHeight;

        return w > 0 && h > 0;
    }

    private void PollFullscreenDoubleClick(double relX, double relY, double w, double h)
    {
        var leftState = GetAsyncKeyState(VkLButton);
        var leftDown = (leftState & unchecked((short)0x8000)) != 0;
        var leftPressedSinceLastPoll = (leftState & 0x0001) != 0;
        var now = DateTime.UtcNow;
        if (ShouldSuppressPollClick(now, leftDown))
        {
            _pollLeftButtonWasDown = leftDown;
            return;
        }

        if (!leftDown && !leftPressedSinceLastPoll)
        {
            _pollLeftButtonWasDown = false;
            return;
        }
        if (_pollLeftButtonWasDown && !leftPressedSinceLastPoll) return;
        _pollLeftButtonWasDown = leftDown;

        if (relX < 0 || relX >= w || relY < 0 || relY >= h) return;
        if (IsInteractiveDoubleClickTarget(Root.InputHitTest(new Point(relX, relY)) as DependencyObject))
            return;

        var current = new Point(relX, relY);
        var maxMs = Math.Max((int)GetDoubleClickTime(), 200);
        var metricDip = GetDoubleClickMetricDip();
        var maxX = Math.Max(metricDip.X, 8.0);
        var maxY = Math.Max(metricDip.Y, 8.0);
        var isDoubleClick =
            (now - _lastPollLeftDownUtc).TotalMilliseconds <= maxMs &&
            Math.Abs(current.X - _lastPollLeftDownPos.X) <= maxX &&
            Math.Abs(current.Y - _lastPollLeftDownPos.Y) <= maxY;

        if (isDoubleClick)
        {
            _lastPollLeftDownUtc = DateTime.MinValue;
            ExecutePlayerDoubleClick("cursor-poll");
            return;
        }

        _lastPollLeftDownUtc = now;
        _lastPollLeftDownPos = current;
    }

    private bool ShouldSuppressPollClick(DateTime now, bool leftDown)
    {
        if (_ignorePollUntilLeftReleased)
        {
            if (!leftDown)
            {
                _ignorePollUntilLeftReleased = false;
                _suppressPollClickUntilUtc = now.AddMilliseconds(120);
            }
            return true;
        }

        if (now < _suppressPollClickUntilUtc)
            return true;

        _suppressPollClickUntilUtc = DateTime.MinValue;
        return false;
    }

    private Point GetDoubleClickMetricDip()
    {
        var px = new Point(GetSystemMetrics(SmCxDoubleClick), GetSystemMetrics(SmCyDoubleClick));
        var src = System.Windows.PresentationSource.FromVisual(this);
        return src?.CompositionTarget is null
            ? px
            : src.CompositionTarget.TransformFromDevice.Transform(px);
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

    private void OnSeekLostCapture(object sender, MouseEventArgs e)
    {
        if (!_seekDragging || Mouse.LeftButton == MouseButtonState.Pressed) return;
        _seekDragging = false;
        if (sender is Slider sl)
            _vm.EndSeek(sl.Value);
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

    private void OnVolumeLostCapture(object sender, MouseEventArgs e)
    {
        if (!_volDragging || Mouse.LeftButton == MouseButtonState.Pressed) return;
        _volDragging = false;
        if (sender is Slider sl)
            _vm.Volume = sl.Value;
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

    /// <summary>SeekBar 위 IN/OUT 핸들 좌표 + range 강조 위치/크기 + 재생 cursor 갱신.</summary>
    private void UpdateTrimOverlay()
    {
        if (TrimOverlay is null || SeekBar is null) return;
        if (!_vm.IsTrimMode || _vm.Duration <= 0) return;
        if (SeekBar.ActualWidth <= 0) return;

        var inCenterX  = SecToX(_vm.TrimInSec  ?? 0);
        var outCenterX = SecToX(_vm.TrimOutSec ?? _vm.Duration);

        System.Windows.Controls.Canvas.SetLeft(TrimInThumb,  inCenterX  - TrimHandleHalf);
        System.Windows.Controls.Canvas.SetLeft(TrimOutThumb, outCenterX - TrimHandleHalf);
        System.Windows.Controls.Canvas.SetLeft(TrimRangeFill, inCenterX);
        TrimRangeFill.Width = Math.Max(0, outCenterX - inCenterX);
        UpdatePlaybackCursor();
    }

    /// <summary>편집 모드 재생 cursor 위치 갱신 (Ellipse 10×10 중앙 정렬).</summary>
    private void UpdatePlaybackCursor()
    {
        if (PlaybackCursor is null || !_vm.IsTrimMode || _vm.Duration <= 0) return;
        if (SeekBar.ActualWidth <= 0) return;
        var x = SecToX(_vm.TimePos);
        System.Windows.Controls.Canvas.SetLeft(PlaybackCursor, x - 5);  // width 10 / 2
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
            BeginOwnedModalInteraction("speed-menu");
            b.ContextMenu.PlacementTarget = b;
            b.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Top;
            b.ContextMenu.Closed -= OnOwnedContextMenuClosed;
            b.ContextMenu.Closed += OnOwnedContextMenuClosed;
            b.ContextMenu.IsOpen = true;
        }
    }

    private void OnOwnedContextMenuClosed(object? sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.ContextMenu menu)
            menu.Closed -= OnOwnedContextMenuClosed;
        if (_ownedModalOpen)
            EndOwnedModalInteraction("context-menu");
    }
    private void OnRootMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled) return;
        if (IsVideoZoomPointerTarget(e.OriginalSource as DependencyObject))
        {
            HandleVideoSurfaceWheel(e.Delta, "root-wheel");
            e.Handled = true;
            return;
        }

        ApplyVolumeWheel(e.Delta);
        e.Handled = true;
    }

    private void HandleVideoSurfaceWheel(int wheelDelta, string source, bool? zoomModifierDown = null)
    {
        if (wheelDelta == 0)
            return;

        if (!Dispatcher.CheckAccess())
        {
            var capturedZoomModifier = zoomModifierDown ?? IsVideoZoomWheelModifierDown();
            Dispatcher.BeginInvoke(
                new Action(() => HandleVideoSurfaceWheel(wheelDelta, source, capturedZoomModifier)),
                DispatcherPriority.Input);
            return;
        }

        if (_vm.CanZoomVideo && (zoomModifierDown ?? IsVideoZoomWheelModifierDown()))
        {
            QueueVideoWheelDelta(wheelDelta, source);
            return;
        }

        ApplyVolumeWheel(wheelDelta);
    }

    private void ApplyVolumeWheel(int wheelDelta)
    {
        _volumeWheelRemainder += wheelDelta;
        var steps = _volumeWheelRemainder / 120;
        if (steps == 0) return;
        _volumeWheelRemainder -= steps * 120;
        _vm.Volume = Math.Clamp(_vm.Volume + steps * 5, 0, 100);
    }

    private static bool IsVideoZoomWheelModifierDown() =>
        IsKeyStateDown(GetAsyncKeyState(VkControl));

    private bool IsVideoZoomPointerTarget(DependencyObject? source)
    {
        if (!_vm.CanZoomVideo) return false;
        if (source is not null && (IsInsideVisual(source, TopBar) || IsInsideVisual(source, BottomBar)))
            return false;

        var pos = Mouse.GetPosition(VideoHostContainer);
        return pos.X >= 0 && pos.Y >= 0 &&
               pos.X <= VideoHostContainer.ActualWidth &&
               pos.Y <= VideoHostContainer.ActualHeight;
    }

    private void QueueVideoWheelDelta(int wheelDelta, string source)
    {
        if (wheelDelta == 0 || !_vm.CanZoomVideo)
            return;

        Interlocked.Add(ref _queuedVideoWheelDelta, wheelDelta);
        if (Interlocked.Exchange(ref _videoWheelDispatchScheduled, 1) == 0)
        {
            Dispatcher.BeginInvoke(
                new Action(() => FlushQueuedVideoWheelDelta(source)),
                DispatcherPriority.Input);
        }
    }

    private void FlushQueuedVideoWheelDelta(string source)
    {
        Interlocked.Exchange(ref _videoWheelDispatchScheduled, 0);
        var wheelDelta = Interlocked.Exchange(ref _queuedVideoWheelDelta, 0);
        if (wheelDelta != 0)
            ApplyVideoZoomFromWheel(wheelDelta, source);

        if (Volatile.Read(ref _queuedVideoWheelDelta) != 0 &&
            Interlocked.Exchange(ref _videoWheelDispatchScheduled, 1) == 0)
        {
            Dispatcher.BeginInvoke(
                new Action(() => FlushQueuedVideoWheelDelta("wheel-queue")),
                DispatcherPriority.Input);
        }
    }

    private void ApplyVideoZoomFromWheel(int wheelDelta, string source)
    {
        if (wheelDelta == 0 || !_vm.CanZoomVideo)
            return;

        ShowControls($"video-zoom-{source}");
        var pointer = VideoHostContainer.PointFromScreen(GetCurrentCursorScreenPoint());
        SetNativeVideoZoomFromWheel(wheelDelta, pointer);
    }

    private void BeginVideoPanDrag(Point screenPoint, string source)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(
                new Action(() => BeginVideoPanDrag(screenPoint, source)),
                DispatcherPriority.Input);
            return;
        }

        if (!_vm.CanZoomVideo || _videoScaleTarget <= VideoHostScaleMin + 0.001)
            return;
        if (_videoPanDragging)
            return;

        _videoPanStartPoint = VideoHostContainer.PointFromScreen(screenPoint);
        _videoPanStartX = _videoPanXTarget;
        _videoPanStartY = _videoPanYTarget;

        _videoPanDragging = true;
        _videoPanUsesPolledButtonState = source == "poll";
        _videoPanSawAsyncButtonDown = IsKeyStateDown(GetAsyncKeyState(VkMButton));
        _videoPanAsyncReleaseMisses = 0;
        _pollMiddleButtonWasDown = true;
        _videoPanLastScreenPoint = screenPoint;
        _queuedVideoPanScreenPoint = screenPoint;
        _queuedVideoPanSource = source;
        Interlocked.Exchange(ref _videoPanDispatchScheduled, 0);
        _lastVideoPanKeepAliveUtc = DateTime.UtcNow;
        DismissPlaylistPanel();
        DismissRecentPanel();
        ShowControls($"video-pan-{source}");
        Mouse.OverrideCursor = Cursors.SizeAll;
        try { Root.Focus(); Keyboard.Focus(Root); } catch { }
        Services.AppLog.Info($"VideoPan[{source}] begin scale={_videoScaleTarget:0.###}");
    }

    private void QueueVideoPanDrag(Point screenPoint, string source)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(
                new Action(() => QueueVideoPanDrag(screenPoint, source)),
                DispatcherPriority.Input);
            return;
        }

        if (!_videoPanDragging)
            return;

        _queuedVideoPanScreenPoint = screenPoint;
        _queuedVideoPanSource = source;
        if (Interlocked.Exchange(ref _videoPanDispatchScheduled, 1) == 0)
        {
            Dispatcher.BeginInvoke(
                new Action(FlushQueuedVideoPanDrag),
                DispatcherPriority.Render);
        }
    }

    private void FlushQueuedVideoPanDrag()
    {
        Interlocked.Exchange(ref _videoPanDispatchScheduled, 0);
        if (!_videoPanDragging)
            return;

        ContinueVideoPanDrag(_queuedVideoPanScreenPoint, _queuedVideoPanSource);
    }

    private void ContinueVideoPanDrag(Point screenPoint, string source)
    {
        if (!_videoPanDragging)
            return;

        _videoPanLastScreenPoint = screenPoint;
        var point = VideoHostContainer.PointFromScreen(screenPoint);
        _videoPanXTarget = _videoPanStartX + (point.X - _videoPanStartPoint.X);
        _videoPanYTarget = _videoPanStartY + (point.Y - _videoPanStartPoint.Y);
        ClampNativeVideoPanTarget();
        _videoPanXCurrent = _videoPanXTarget;
        _videoPanYCurrent = _videoPanYTarget;
        ApplyNativeVideoTransform();
        KeepVideoPanDragAlive(source);
    }

    private void KeepVideoPanDragAlive(string source)
    {
        var now = DateTime.UtcNow;
        if (now - _lastVideoPanKeepAliveUtc < TimeSpan.FromMilliseconds(VideoPanKeepAliveThrottleMs))
            return;

        _lastVideoPanKeepAliveUtc = now;
        ShowControls(source);
        Mouse.OverrideCursor = Cursors.SizeAll;
    }

    private bool PollVideoPanDrag(Point screenPoint)
    {
        var state = GetAsyncKeyState(VkMButton);
        var middleDown = IsKeyStateDown(state);
        var middlePressedSinceLastPoll = (state & 0x0001) != 0;

        if (_videoPanDragging)
        {
            if (middleDown)
            {
                _videoPanSawAsyncButtonDown = true;
                _videoPanAsyncReleaseMisses = 0;
            }
            // Remapped input은 async state가 처음부터 false일 수 있으므로 explicit Up을
            // 기다린다. 실제 physical down을 한 번이라도 본 gesture만 2회 연속 release로
            // 보조 종료해 hook/capture 손실 시 cursor가 고정되는 것도 막는다.
            else if ((_videoPanUsesPolledButtonState || _videoPanSawAsyncButtonDown) &&
                     ++_videoPanAsyncReleaseMisses >= 2)
            {
                EndVideoPanDrag("middle-up");
                return false;
            }

            var dx = screenPoint.X - _videoPanLastScreenPoint.X;
            var dy = screenPoint.Y - _videoPanLastScreenPoint.Y;
            if (Math.Abs(dx) > 0.5 || Math.Abs(dy) > 0.5)
                QueueVideoPanDrag(screenPoint, "video-pan-poll");
            return true;
        }

        if (middleDown &&
            (middlePressedSinceLastPoll || !_pollMiddleButtonWasDown) &&
            IsActive &&
            IsCursorOverNativeVideoSurface(screenPoint))
        {
            BeginVideoPanDrag(screenPoint, "poll");
            _pollMiddleButtonWasDown = middleDown;
            return _videoPanDragging;
        }

        _pollMiddleButtonWasDown = middleDown;
        return false;
    }

    private static bool IsKeyStateDown(short state) =>
        (state & unchecked((short)0x8000)) != 0;

    private void EndVideoPanDrag(string source)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(
                new Action(() => EndVideoPanDrag(source)),
                DispatcherPriority.Input);
            return;
        }

        if (!_videoPanDragging) return;
        _videoPanDragging = false;
        _videoPanUsesPolledButtonState = false;
        _videoPanSawAsyncButtonDown = false;
        _videoPanAsyncReleaseMisses = 0;
        _pollMiddleButtonWasDown = false;
        Interlocked.Exchange(ref _videoPanDispatchScheduled, 0);
        Mouse.OverrideCursor = null;
        Services.AppLog.Info($"VideoPan[{source}] end");
    }

    private void SetNativeVideoZoomFromWheel(int wheelDelta, Point pointer)
    {
        var width = VideoHostContainer.ActualWidth;
        var height = VideoHostContainer.ActualHeight;
        if (width <= 0 || height <= 0)
            return;

        var oldScale = _videoScaleTarget;
        var notches = wheelDelta / 120.0;
        var nextScale = Math.Clamp(oldScale * Math.Pow(VideoHostWheelFactor, notches),
            VideoHostScaleMin, VideoHostScaleMax);
        if (nextScale <= 1.01)
            nextScale = VideoHostScaleMin;
        if (Math.Abs(nextScale - oldScale) <= 0.0001)
            return;

        _videoScaleTarget = nextScale;
        if (nextScale <= VideoHostScaleMin + 0.001)
        {
            _videoPanXTarget = 0;
            _videoPanYTarget = 0;
        }
        else
        {
            var safeOldScale = Math.Max(VideoHostScaleMin, oldScale);
            var (contentX, contentY, contentWidth, contentHeight) = GetNativeVideoContentRect(width, height);
            var offsetX = Math.Clamp(pointer.X, contentX, contentX + contentWidth) - width / 2.0;
            var offsetY = Math.Clamp(pointer.Y, contentY, contentY + contentHeight) - height / 2.0;
            var ratio = nextScale / safeOldScale;
            _videoPanXTarget = (_videoPanXTarget - offsetX) * ratio + offsetX;
            _videoPanYTarget = (_videoPanYTarget - offsetY) * ratio + offsetY;
            ClampNativeVideoPanTarget();
        }

        ApplyNativeVideoTransformImmediately();
    }

    private void ApplyNativeVideoTransformImmediately()
    {
        _videoScaleCurrent = _videoScaleTarget;
        _videoPanXCurrent = _videoPanXTarget;
        _videoPanYCurrent = _videoPanYTarget;
        ApplyNativeVideoTransform();
    }

    private void ResetNativeVideoTransform()
    {
        _videoScaleCurrent = _videoScaleTarget = VideoHostScaleMin;
        _videoPanXCurrent = _videoPanYCurrent = 0;
        _videoPanXTarget = _videoPanYTarget = 0;
        _videoHost.FitVideoViewportToHost();
        _vm.SetRendererVideoTransform(VideoHostScaleMin, 0, 0);
    }

    private void ClampNativeVideoPanTarget()
    {
        var width = VideoHostContainer.ActualWidth;
        var height = VideoHostContainer.ActualHeight;
        if (_videoScaleTarget <= VideoHostScaleMin + 0.001 || width <= 0 || height <= 0)
        {
            _videoPanXTarget = 0;
            _videoPanYTarget = 0;
            return;
        }

        var (_, _, contentWidth, contentHeight) = GetNativeVideoContentRect(width, height);
        var maxX = Math.Max(0, (contentWidth * _videoScaleTarget - width) / 2.0);
        var maxY = Math.Max(0, (contentHeight * _videoScaleTarget - height) / 2.0);
        _videoPanXTarget = Math.Clamp(_videoPanXTarget, -maxX, maxX);
        _videoPanYTarget = Math.Clamp(_videoPanYTarget, -maxY, maxY);
    }

    private void ApplyNativeVideoTransform()
    {
        var width = VideoHostContainer.ActualWidth;
        var height = VideoHostContainer.ActualHeight;
        if (width <= 0 || height <= 0)
            return;
        var (_, _, contentWidth, contentHeight) = GetNativeVideoContentRect(width, height);
        // mpv video-pan-x/y의 단위는 확대된 영상 전체 크기의 비율이다.
        // viewport로 나누면 확대 배율만큼 과도하게 이동해 검은 영역이 노출된다.
        var scaledContentWidth = Math.Max(1.0, contentWidth * _videoScaleCurrent);
        var scaledContentHeight = Math.Max(1.0, contentHeight * _videoScaleCurrent);
        var panX = _videoPanXCurrent / scaledContentWidth;
        var panY = _videoPanYCurrent / scaledContentHeight;
        _vm.SetRendererVideoTransform(_videoScaleCurrent, panX, panY);
    }

    private (double X, double Y, double Width, double Height) GetNativeVideoContentRect(
        double viewportWidth,
        double viewportHeight)
    {
        var aspectRatio = _vm.VideoDisplayAspectRatio;
        if (aspectRatio <= 0 || double.IsNaN(aspectRatio) || double.IsInfinity(aspectRatio))
            return (0, 0, viewportWidth, viewportHeight);

        var viewportAspectRatio = viewportWidth / Math.Max(1, viewportHeight);
        double width;
        double height;
        if (viewportAspectRatio > aspectRatio)
        {
            height = viewportHeight;
            width = height * aspectRatio;
        }
        else
        {
            width = viewportWidth;
            height = width / aspectRatio;
        }

        return ((viewportWidth - width) / 2.0, (viewportHeight - height) / 2.0, width, height);
    }

    private void OnRootDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        if (IsInteractiveDoubleClickTarget(e.OriginalSource as DependencyObject)) return;

        ExecutePlayerDoubleClick("wpf");
        e.Handled = true;
    }

    private void ExecutePlayerDoubleClick(string source)
    {
        switch (PlayerInteractionPolicy.DoubleClickAction(_vm.State, _vm.HasMedia))
        {
            case PlayerDoubleClickAction.OpenFile:
                if (_vm.OpenCommand.CanExecute(null))
                    _vm.OpenCommand.Execute(null);
                return;
            case PlayerDoubleClickAction.ToggleFullscreen:
                RequestFullscreenToggle(source, FullscreenRequestKind.DoubleClick);
                return;
            default:
                return;
        }
    }

    private void OnFullscreenButtonClick(object? sender, RoutedEventArgs e)
    {
        RequestFullscreenToggle("button", FullscreenRequestKind.Control);
        e.Handled = true;
    }

    private void OnImmersiveFullscreenButtonClick(object? sender, RoutedEventArgs e)
    {
        if (_vm.IsFullscreen)
            HideControls(force: true, source: "immersive-button");
        e.Handled = true;
    }

    private enum FullscreenRequestKind
    {
        Control,
        DoubleClick,
        Keyboard,
        WindowButton
    }

    private void OnFullscreenShortcutKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var modifiers = Keyboard.Modifiers;
        var noModifiers = modifiers == ModifierKeys.None;
        var altOnly = modifiers == ModifierKeys.Alt;

        if (noModifiers && key == Key.F1)
        {
            OpenHelpWindow();
            e.Handled = true;
            return;
        }

        if (key == Key.Escape)
        {
            if (_vm.IsTrimMode)
            {
                _vm.CancelTrimModeCommand?.Execute(null);
                e.Handled = true;
                return;
            }

            if (_vm.IsFullscreen)
            {
                RequestFullscreenState(false, "keyboard-esc", FullscreenRequestKind.Keyboard);
                e.Handled = true;
            }
            return;
        }

        var isBareEnter =
            noModifiers && (key == Key.Enter || key == Key.Return);
        if (isBareEnter && IsKeyboardActionFocused())
            return;

        var isFullscreenShortcut =
            (noModifiers && (key == Key.F || key == Key.F11 || key == Key.Enter || key == Key.Return)) ||
            (altOnly && (key == Key.Enter || key == Key.Return));

        if (!isFullscreenShortcut) return;

        RequestFullscreenToggle("keyboard", FullscreenRequestKind.Keyboard);
        e.Handled = true;
    }

    private bool RequestFullscreenToggle(string source, FullscreenRequestKind kind)
    {
        return RequestFullscreenState(!_vm.IsFullscreen, source, kind);
    }

    private bool RequestFullscreenState(bool targetFullscreen, string source, FullscreenRequestKind kind)
    {
        var now = DateTime.UtcNow;
        if (kind == FullscreenRequestKind.DoubleClick &&
            (now - _lastDoubleClickToggleUtc).TotalMilliseconds < FullscreenDoubleClickDuplicateSuppressMs)
        {
            Services.AppLog.Info($"FullscreenRequest[{source}] ignored duplicate kind={kind} fs={_vm.IsFullscreen}");
            return false;
        }

        if (targetFullscreen == _vm.IsFullscreen)
        {
            Services.AppLog.Info($"FullscreenRequest[{source}] ignored no-op kind={kind} fs={_vm.IsFullscreen}");
            ResetFullscreenPointerTracking(suppressPollingUntilRelease: Mouse.LeftButton == MouseButtonState.Pressed);
            return false;
        }

        if (kind == FullscreenRequestKind.DoubleClick)
            _lastDoubleClickToggleUtc = now;

        ResetFullscreenPointerTracking(suppressPollingUntilRelease: true);
        Services.AppLog.Info(
            $"FullscreenRequest[{source}] {(_vm.IsFullscreen ? "exit" : "enter")} -> {targetFullscreen} kind={kind}");
        _vm.IsFullscreen = targetFullscreen;
        return true;
    }

    private void ResetFullscreenPointerTracking(bool suppressPollingUntilRelease)
    {
        _pollLeftButtonWasDown = false;
        _lastPollLeftDownUtc = DateTime.MinValue;
        if (!suppressPollingUntilRelease) return;
        _ignorePollUntilLeftReleased = true;
        _suppressPollClickUntilUtc = DateTime.UtcNow.AddMilliseconds(120);
    }

    private static bool IsInteractiveDoubleClickTarget(DependencyObject? d)
    {
        while (d is not null)
        {
            if (d is ButtonBase or Slider or Selector or TextBoxBase or ScrollBar or Thumb)
                return true;
            d = VisualTreeHelper.GetParent(d);
        }
        return false;
    }

    private static bool IsInsideVisual(DependencyObject? d, DependencyObject target)
    {
        while (d is not null)
        {
            if (ReferenceEquals(d, target)) return true;
            d = VisualTreeHelper.GetParent(d);
        }
        return false;
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
        _ignoreStationaryFullscreenReveal = false;
        ShowControls("key");
    }


    // ============================================================
    // Space: 짧게 누름 = Play/Pause, 0.5초 이상 hold = 2x 재생 (놓으면 원래 속도)
    // IsRepeat(OS 키 auto-repeat 간격 가변) 대신 정확한 0.5초 timer를 사용한다.
    // ============================================================
    private bool _spaceHeld;
    private double _speedBeforeHold = 1.0;
    private DispatcherTimer? _spaceHoldTimer;

    private void OnSpaceDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Space) return;
        if (IsKeyboardActionFocused()) return;
        _ignoreStationaryFullscreenReveal = false;
        ShowControls("space-down");
        e.Handled = true;
        if (e.IsRepeat) return;

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
        ShowControls("space-hold");
    }

    private void OnSpaceUp(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Space) return;
        if (!_spaceHeld && _spaceHoldTimer?.IsEnabled != true && IsKeyboardActionFocused())
            return;
        e.Handled = true;
        EndSpaceHoldOrToggle();
    }

    private static bool IsKeyboardActionFocused() =>
        Keyboard.FocusedElement is ButtonBase or Slider or TextBoxBase or Selector or MenuItem;

    /// <summary>0.5초 안에 release → play/pause 단발. 이후 release → speed 복원.</summary>
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
            BeginOwnedModalInteraction("settings");
            var dlg = new SettingsWindow(_vm.Settings) { Owner = this };
            dlg.ShowDialog();
            EndOwnedModalInteraction("settings");
        }
        catch (Exception ex)
        {
            Services.AppLog.Error("Open SettingsWindow", ex);
            MessageBox.Show(this, LocalizationService.F("SettingsOpenFailed", ex.Message),
                "Deno Video Player", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            if (_ownedModalOpen)
                EndOwnedModalInteraction("settings-finally");
        }
    }

    private void OnHelpClicked(object? sender, RoutedEventArgs e)
    {
        OpenHelpWindow();
        e.Handled = true;
    }

    private void OnTroubleshootingClicked(object? sender, RoutedEventArgs e)
    {
        OpenHelpWindow(showTroubleshooting: true);
        e.Handled = true;
    }

    private async void OnRetryPlaybackEngineClicked(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (_closing || Volatile.Read(ref _backendOperationInFlight) != 0)
            return;

        var mediaToResume = _vm.CurrentMedia;
        _mpvProc.Dispose();
        _vm.State = PlayerState.Loading;
        _vm.SetLocalizedStatus("FirstRunStageChecking");
        await InitializePlaybackBackendAsync(mediaToResume);
    }

    private void OpenHelpWindow(bool showTroubleshooting = false)
    {
        ShowControls("help");
        DismissPlaylistPanel();
        DismissRecentPanel();
        EndVideoPanDrag("help-open");

        if (_helpWin is null)
        {
            var help = new HelpWindow { Owner = this };
            help.Closed += (_, _) =>
            {
                if (ReferenceEquals(_helpWin, help))
                    _helpWin = null;
            };
            _helpWin = help;
            help.Show();
        }
        else
        {
            if (_helpWin.WindowState == WindowState.Minimized)
                _helpWin.WindowState = WindowState.Normal;
            if (!_helpWin.IsVisible)
                _helpWin.Show();
        }

        _helpWin.Activate();
        if (showTroubleshooting)
            _helpWin.ScrollToTroubleshooting();
    }

    private void OnMinimize(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void OnMaxRestore(object? sender, RoutedEventArgs e)
    {
        var now = DateTime.UtcNow;
        if ((now - _lastMaxRestoreClickUtc).TotalMilliseconds < WindowButtonDebounceMs)
        {
            e.Handled = true;
            return;
        }
        _lastMaxRestoreClickUtc = now;

        ToggleWindowMaximizeRestore("max-restore");
        e.Handled = true;
    }

    private void ToggleWindowMaximizeRestore(string source)
    {
        if (_vm.IsFullscreen)
        {
            RequestFullscreenState(false, source, FullscreenRequestKind.WindowButton);
            return;
        }

        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
        UpdateMaxRestoreButton();
        Services.AppLog.Info($"WindowState[{source}] -> {WindowState}");
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();

    private void UpdateMaxRestoreButton()
    {
        if (MaxRestoreBtn is null) return;
        MaxRestoreBtn.Content = _vm?.IsFullscreen == true || WindowState == WindowState.Maximized
            ? "\uE923"
            : "\uE922";
    }

    // ============================================================
    // Fullscreen
    // ============================================================
    // 풀스크린 진입/탈출 — 200ms cubic ease 애니메이션으로 부드럽게 grow/shrink.
    // 핵심:
    //   - WindowChrome SetWindowChrome 호출 안 함 (XAML 한 번 set으로 유지)
    //   - WindowStyle=None을 계속 유지해 Windows 기본 캡션 버튼이 다시 뜨는 경로 차단
    //   - Bounds 4개 (Left/Top/Width/Height)를 DoubleAnimation으로 동시에 interpolate
    //     → WPF가 매 frame layout, mpv child hwnd가 자연스럽게 따라옴
    //   - WindowState 토글 안 함 (Maximized hack 제거)
    private void ApplyFullscreen(bool fs)
    {
        ResetFullscreenPointerTracking(suppressPollingUntilRelease: Mouse.LeftButton == MouseButtonState.Pressed);
        DismissPlaylistPanel();
        DismissRecentPanel();
        UpdateEffectiveTopmost();

        if (fs)
        {
            UpdateMediaLayerForFullscreen(immersive: false);
            ShowControls("fullscreen-enter");
            try { Root.Focus(); Keyboard.Focus(Root); } catch { }

            var isVisuallyMaximized = WindowState == WindowState.Maximized;
            if (!_hasFullscreenRestoreBounds)
                CaptureFullscreenRestoreBounds(isVisuallyMaximized);

            if (isVisuallyMaximized)
            {
                // Maximized면 RestoreBounds로 visual 위치를 먼저 잡고 거기서 시작.
                // 안 그러면 Maximized의 가상 bounds(-8,-8,fullW+16,fullH+16)에서
                // 모니터로 점프 → 어색.
                WindowState = WindowState.Normal;
                Left = _savedLeft; Top = _savedTop;
                Width = _savedWidth; Height = _savedHeight;
            }

            EnsureCustomWindowStyle();
            SetResizeBorderForFullscreen(true);
            ResizeMode = ResizeMode.NoResize;

            var mb = GetCurrentMonitorBounds();
            AnimateBounds(mb.X, mb.Y, mb.Width, mb.Height, durationMs: 140,
                onCompleted: () =>
                {
                    SyncPlaylistWindowPosition();
                    SyncRecentWindowPosition();
                    ShowFullscreenControlsOnEntry();
                });
        }
        else
        {
            _ignoreStationaryFullscreenReveal = false;
            Mouse.OverrideCursor = null;
            UpdateMediaLayerForFullscreen(immersive: false);
            SetResizeBorderForFullscreen(false);
            // 먼저 bounds를 saved로 animate, 끝난 후 resize/maximize 상태만 복원
            var tx = _savedLeft;
            var ty = _savedTop;
            var tw = _savedWidth  > 0 ? _savedWidth  : 1280;
            var th = _savedHeight > 0 ? _savedHeight : 760;
            AnimateBounds(tx, ty, tw, th, durationMs: 140, onCompleted: () =>
            {
                EnsureCustomWindowStyle();
                UpdateMediaLayerForFullscreen(immersive: false);
                ResizeMode = _savedResize == ResizeMode.NoResize ? ResizeMode.CanResize : _savedResize;
                if (_savedWasMaximized)
                    WindowState = WindowState.Maximized;
                _hasFullscreenRestoreBounds = false;
                SyncPlaylistWindowPosition();
                SyncRecentWindowPosition();
            });
            ShowControls("fullscreen-exit");
        }
        SyncPlaylistWindowPosition();
        SyncRecentWindowPosition();
    }

    private void UpdateEffectiveTopmost()
    {
        var effectiveTopmost = FullscreenWindowPolicy.ShouldBeTopmost(
            _vm.IsFullscreen,
            IsCurrentProcessForeground(),
            _vm.IsAlwaysOnTop);

        if (Topmost != effectiveTopmost)
            Topmost = effectiveTopmost;
        if (_playlistWin is not null)
            _playlistWin.Topmost = effectiveTopmost;
        if (_recentWin is not null)
            _recentWin.Topmost = effectiveTopmost;
    }

    private static bool IsCurrentProcessForeground()
    {
        var foregroundWindow = GetForegroundWindow();
        if (foregroundWindow == IntPtr.Zero)
            return false;

        GetWindowThreadProcessId(foregroundWindow, out var processId);
        return processId == (uint)Environment.ProcessId;
    }

    private void CaptureFullscreenRestoreBounds(bool isVisuallyMaximized)
    {
        _savedResize = ResizeMode;
        _savedWasMaximized = isVisuallyMaximized;
        if (isVisuallyMaximized)
        {
            var rb = RestoreBounds;
            _savedLeft = double.IsNaN(rb.Left) ? 100 : rb.Left;
            _savedTop = double.IsNaN(rb.Top) ? 60 : rb.Top;
            _savedWidth = rb.Width > 480 ? rb.Width : 1280;
            _savedHeight = rb.Height > 320 ? rb.Height : 760;
        }
        else
        {
            _savedLeft = Left;
            _savedTop = Top;
            _savedWidth = Width;
            _savedHeight = Height;
        }
        _hasFullscreenRestoreBounds = true;
    }

    private void EnsureCustomWindowStyle()
    {
        if (WindowStyle != WindowStyle.None)
            WindowStyle = WindowStyle.None;
    }

    private void UpdateMediaLayerForFullscreen(bool immersive)
    {
        Grid.SetRow(MediaLayer, immersive ? 0 : 1);
        Grid.SetRowSpan(MediaLayer, immersive ? 3 : 1);
        Panel.SetZIndex(MediaLayer, 0);
        Panel.SetZIndex(TopBar, 20);
        Panel.SetZIndex(BottomBar, 20);
    }

    private void SetResizeBorderForFullscreen(bool fullscreen)
    {
        var chrome = System.Windows.Shell.WindowChrome.GetWindowChrome(this);
        if (chrome is null) return;
        chrome.ResizeBorderThickness = fullscreen
            ? new Thickness(0)
            : new Thickness(WindowResizeBorderDip);
    }

    /// <summary>
    /// 윈도우 Bounds (Left/Top/Width/Height)를 부드럽게 animate. mpv child hwnd가 매
    /// frame 따라가서 grow/shrink 자연스러움. 이전 animation 진행 중이면 cancel하고
    /// 현재 값에서 새로 시작.
    /// </summary>
    private void AnimateBounds(double targetX, double targetY, double targetW, double targetH,
        double durationMs, Action? onCompleted = null)
    {
        var serial = ++_boundsAnimationSerial;
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
            if (serial != _boundsAnimationSerial) return;
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

    // 현재 윈도우가 있는 모니터의 작업 영역 (taskbar 제외, DIP 단위).
    internal Rect GetCurrentMonitorWorkAreaBounds()
    {
        try
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            if (TryGetMonitorWorkArea(hwnd, out var workArea))
            {
                var src = System.Windows.PresentationSource.FromVisual(this);
                var dpiX = src?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
                var dpiY = src?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;
                return new Rect(workArea.Left / dpiX, workArea.Top / dpiY,
                    (workArea.Right - workArea.Left) / dpiX,
                    (workArea.Bottom - workArea.Top) / dpiY);
            }
        }
        catch (Exception ex)
        {
            Services.AppLog.Warn($"GetCurrentMonitorWorkAreaBounds failed: {ex.Message}");
        }
        return SystemParameters.WorkArea;
    }

    // 현재 윈도우가 있는 모니터의 전체 bounds (DPI scale 적용된 DIP 단위).
    private Rect GetCurrentMonitorBounds()
    {
        try
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            var mh = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
            var mi = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (GetMonitorInfo(mh, ref mi))
            {
                var src = System.Windows.PresentationSource.FromVisual(this);
                var dpiX = src?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
                var dpiY = src?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;
                var r = mi.Monitor;
                return new Rect(r.Left / dpiX, r.Top / dpiY,
                    (r.Right - r.Left) / dpiX, (r.Bottom - r.Top) / dpiY);
            }
        }
        catch (Exception ex)
        {
            Services.AppLog.Warn($"GetCurrentMonitorBounds failed: {ex.Message}");
        }
        return new Rect(0, 0, SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight);
    }

}

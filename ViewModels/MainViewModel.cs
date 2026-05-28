using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using DenoVideoPlayer.Helpers;
using DenoVideoPlayer.Models;
using DenoVideoPlayer.Services;

namespace DenoVideoPlayer.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly MpvProcessService _mpvProc;
    private readonly MpvIpcClient _ipc = new();
    private readonly PlaylistService _playlistSvc = new();
    private readonly SettingsService _settingsSvc = new();
    private readonly System.Windows.Threading.Dispatcher _ui;

    public AppSettings Settings { get; private set; }
    public ObservableCollection<MediaItem> Playlist { get; } = new();
    public ObservableCollection<RecentItem> Recents { get; } = new();
    private const int MaxRecents = 30;

    // (MouseActivity / MpvMousePos events 폐기 — 이제 MainWindow가 GetCursorPos polling으로 직접 처리.)

    /// <summary>잠깐 띄울 OSD toast. 스크린샷 저장 같은 짧은 confirm용.</summary>
    public event Action<string>? Toast;

    public MainViewModel(MpvProcessService mpvProc)
    {
        _mpvProc = mpvProc;
        _ui = Application.Current.Dispatcher;
        Settings = _settingsSvc.Load();
        _volume = Settings.Volume;
        _muted = Settings.Muted;
        _speed = Settings.PlaybackRate;
        _isPlaylistOpen = Settings.PlaylistPanelEnabled;
        _repeat = Settings.RepeatMode is >= 0 and <= 2 ? (RepeatMode)Settings.RepeatMode : RepeatMode.None;
        _shuffle = Settings.Shuffle;
        LocalizationService.LanguageChanged += OnLanguageChanged;

        // 최근 재생 목록 복원 — 파일 존재 여부와 무관하게 다 보여줌 (열 때 파일 없으면 Failed로)
        if (Settings.RecentFiles is { Count: > 0 })
        {
            foreach (var p in Settings.RecentFiles)
                Recents.Add(new RecentItem(p));
        }

        PlayPauseCommand = new RelayCommand(TogglePlayPause);
        NextCommand      = new RelayCommand(Next, () => HasNext);
        PrevCommand      = new RelayCommand(Prev, () => HasPrev);
        OpenCommand      = new RelayCommand(OpenDialog);
        OpenFolderCommand = new RelayCommand(OpenFolderDialog);
        MuteCommand      = new RelayCommand(() => IsMuted = !IsMuted);
        FullscreenCommand= new RelayCommand(() => IsFullscreen = !IsFullscreen);
        ExitFullscreenCommand = new RelayCommand(() =>
        {
            // ESC 우선순위: 편집 모드 > 풀스크린. 둘 다 활성이면 편집 모드만 빠짐.
            if (IsTrimMode) { CancelTrimModeCommand?.Execute(null); return; }
            if (IsFullscreen) IsFullscreen = false;
        });
        ScreenshotCommand= new RelayCommand(TakeScreenshot);
        AlwaysOnTopCommand=new RelayCommand(() => IsAlwaysOnTop = !IsAlwaysOnTop);
        TogglePlaylistCommand = new RelayCommand(() => IsPlaylistOpen = !IsPlaylistOpen);
        CycleRepeatCommand = new RelayCommand(() => Repeat = Repeat switch
        {
            RepeatMode.None      => RepeatMode.RepeatAll,
            RepeatMode.RepeatAll => RepeatMode.RepeatOne,
            _                    => RepeatMode.None
        });
        SetRepeatNoneCommand = new RelayCommand(() => Repeat = RepeatMode.None);
        SetRepeatAllCommand  = new RelayCommand(() => Repeat = RepeatMode.RepeatAll);
        SetRepeatOneCommand  = new RelayCommand(() => Repeat = RepeatMode.RepeatOne);
        ToggleShuffleCommand = new RelayCommand(() => Shuffle = !Shuffle);
        Seek5ForwardCommand   = new RelayCommand(() => SeekBy(5));
        Seek5BackwardCommand  = new RelayCommand(() => SeekBy(-5));
        Seek30ForwardCommand  = new RelayCommand(() => SeekBy(30));
        Seek30BackwardCommand = new RelayCommand(() => SeekBy(-30));
        VolumeUpCommand   = new RelayCommand(() => Volume = Math.Min(100, Volume + 5));
        VolumeDownCommand = new RelayCommand(() => Volume = Math.Max(0,   Volume - 5));
        FrameStepCommand  = new RelayCommand(() => _ = _ipc.FrameStep());
        FrameBackCommand  = new RelayCommand(() => _ = _ipc.FrameBackStep());
        IncreaseSpeedCommand = new RelayCommand(() => Speed = Math.Min(4.0, Math.Round(Speed + 0.25, 2)));
        DecreaseSpeedCommand = new RelayCommand(() => Speed = Math.Max(0.25, Math.Round(Speed - 0.25, 2)));
        ResetSpeedCommand    = new RelayCommand(() => SetSpeedForce(1.0));
        // 메뉴 preset에서 호출 — CommandParameter로 문자열 "0.5"/"1.25" 등 전달, 안전하게 parse 후 force-set.
        SetSpeedCommand      = new RelayCommand(p =>
        {
            if (p is double d) { SetSpeedForce(d); return; }
            if (p is string s && double.TryParse(s, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var v)) SetSpeedForce(v);
        });
        ApplyUpdateCommand = new RelayCommand(_ => _ = ApplyPendingUpdateAsync(),
            _ => IsUpdateAvailable && !_applyingUpdate);

        // 자막 / 오디오 트랙 — mpv 측 사이클. UI에 트랙 list까지 띄우면 무거워지므로 키만.
        // 미디어 없을 때 toast가 misleading하지 않게 가드. 자막은 비디오 전용, 오디오 사이클은 오디오/비디오 둘 다.
        CycleSubtitleCommand           = new RelayCommand(() =>
        {
            if (CurrentMedia is null || CurrentMedia.Kind != MediaKind.Video)
            { Toast?.Invoke(L("NoSubtitleForMedia")); return; }
            _ = _ipc.CycleSubtitle();           Toast?.Invoke(L("SubtitleTrackNext"));
        });
        ToggleSubtitleVisibilityCommand= new RelayCommand(() =>
        {
            if (CurrentMedia is null || CurrentMedia.Kind != MediaKind.Video)
            { Toast?.Invoke(L("NoSubtitleForMedia")); return; }
            _ = _ipc.CycleSubtitleVisibility(); Toast?.Invoke(L("SubtitleVisibilityToggle"));
        });
        CycleAudioCommand              = new RelayCommand(() =>
        {
            if (CurrentMedia is null || CurrentMedia.Kind == MediaKind.Image)
            { Toast?.Invoke(L("NoPlayingMedia")); return; }
            _ = _ipc.CycleAudio();              Toast?.Invoke(L("AudioTrackNext"));
        });

        // ─── 잘라내기 (ffmpeg stream copy, 무손실, 키프레임 단위) ───
        SetTrimInCommand = new RelayCommand(() =>
        {
            if (CurrentMedia is null || CurrentMedia.Kind == MediaKind.Image)
            { Toast?.Invoke(L("ImageCannotTrim")); return; }
            TrimInSec = TimePos;
            Toast?.Invoke($"IN  {Helpers.TimeFormat.Seconds(TimePos)}");
        });
        SetTrimOutCommand = new RelayCommand(() =>
        {
            if (CurrentMedia is null || CurrentMedia.Kind == MediaKind.Image)
            { Toast?.Invoke(L("ImageCannotTrim")); return; }
            TrimOutSec = TimePos;
            Toast?.Invoke($"OUT {Helpers.TimeFormat.Seconds(TimePos)}");
        });
        ClearTrimCommand = new RelayCommand(() =>
        {
            TrimInSec = null; TrimOutSec = null;
            Toast?.Invoke(L("TrimPointsCleared"));
        });
        ExecuteTrimCommand = new RelayCommand(async _ =>
        {
            var saved = await ExecuteTrimAsync();
            if (saved)
            {
                IsTrimMode = false;
                _ = _ipc.ClearAbLoop();
            }
        }, _ => CanExecuteTrim());

        // 가위 버튼 = 편집 모드 토글. !IsTrimMode면 진입 (IN=0, OUT=Duration 기본),
        // IsTrimMode && HasTrimRange면 실행 후 exit, IsTrimMode && !HasTrimRange면 cancel.
        ToggleTrimModeCommand = new RelayCommand(async _ =>
        {
            if (CurrentMedia is null) { Toast?.Invoke(L("NoPlayingMedia")); return; }
            if (CurrentMedia.Kind == MediaKind.Image) { Toast?.Invoke(L("ImageCannotTrim")); return; }
            if (_trimBusy) return;

            if (!IsTrimMode)
            {
                // 진입: 기본 IN=0, OUT=Duration (사용자가 양쪽 핸들 드래그로 조정)
                TrimInSec = 0;
                TrimOutSec = Duration > 0 ? Duration : null;
                IsTrimMode = true;
                ApplyTrimLoop();  // mpv ab-loop set → IN~OUT 자동 루프
                Toast?.Invoke(L("TrimModeEntered"));
            }
            else
            {
                // 가위 두 번째 클릭 = 편집 모드 취소 (저장 안 함).
                // 저장은 별도 Save 버튼이 ExecuteTrimCommand 호출.
                IsTrimMode = false;
                TrimInSec = null; TrimOutSec = null;
                _ = _ipc.ClearAbLoop();  // mpv 구간 루프 해제
                Toast?.Invoke(L("TrimModeExited"));
                await Task.CompletedTask;
            }
        });
        CancelTrimModeCommand = new RelayCommand(() =>
        {
            if (!IsTrimMode) return;
            IsTrimMode = false;
            TrimInSec = null; TrimOutSec = null;
            _ = _ipc.ClearAbLoop();
            Toast?.Invoke(L("TrimModeCanceled"));
        });

        _ipc.PropertyChanged += OnMpvPropertyChanged;
        _ipc.EndFile        += OnEndFile;
        _ipc.FileLoaded     += OnFileLoaded;
        _ipc.Connected      += OnIpcConnected;
        _ipc.Disconnected   += () => OnUi(() =>
        {
            // Failed 상태(예: mpv crash 메시지)는 보존. 그 외엔 NoFile로.
            if (State != PlayerState.Failed) State = PlayerState.NoFile;
        });
    }

    public async Task ConnectIpcAsync()
    {
        Services.AppLog.Info($"IPC connect attempt pipe={_mpvProc.PipeName}");
        var ok = await _ipc.ConnectAsync(_mpvProc.PipeName, TimeSpan.FromSeconds(5));
        if (!ok)
        {
            Services.AppLog.Error("IPC connect failed (timeout)");
            StatusMessage = L("MpvIpcFailed");
            State = PlayerState.Failed;
            return;
        }
        Services.AppLog.Info("IPC connected");
    }

    private async void OnIpcConnected()
    {
        // 초기 상태 적용 + 관찰 등록
        try
        {
            await _ipc.SetVolume(Volume);
            await _ipc.SetMute(IsMuted);
            await _ipc.SetSpeed(Speed);
            // 시작 시 mpv loop-file을 우리 Repeat 모드와 sync (settings에서 RepeatOne 복원된 경우 등)
            await _ipc.CommandAsync("set_property", "loop-file",
                _repeat == RepeatMode.RepeatOne ? "inf" : "no");

            await _ipc.ObserveProperty("time-pos");
            await _ipc.ObserveProperty("duration");
            await _ipc.ObserveProperty("pause");
            await _ipc.ObserveProperty("volume");
            await _ipc.ObserveProperty("mute");
            await _ipc.ObserveProperty("speed");
            await _ipc.ObserveProperty("filename");
            await _ipc.ObserveProperty("media-title");
            await _ipc.ObserveProperty("eof-reached");
            await _ipc.ObserveProperty("file-format");
            await _ipc.ObserveProperty("seekable");
            // mouse-pos는 더 이상 observe 안 함 — GetCursorPos polling으로 우회
        }
        catch { /* IPC 일찍 끊겨도 앱 계속 */ }
    }

    // ============================================================
    // public bindable state
    // ============================================================

    private PlayerState _state = PlayerState.NoFile;
    public PlayerState State
    {
        get => _state;
        set
        {
            if (!Set(ref _state, value)) return;
            Raise(nameof(IsAudioPlayback));
            Raise(nameof(IsVideoSurfaceVisible));
        }
    }

    private string _statusMessage = "";
    public string StatusMessage { get => _statusMessage; set => Set(ref _statusMessage, value); }

    private string _fileNameDisplay = "";
    public string FileNameDisplay { get => _fileNameDisplay; private set => Set(ref _fileNameDisplay, value); }

    private string _folderDisplay = "";
    public string FolderDisplay { get => _folderDisplay; private set => Set(ref _folderDisplay, value); }

    private MediaItem? _currentMedia;
    public MediaItem? CurrentMedia
    {
        get => _currentMedia;
        private set
        {
            if (!Set(ref _currentMedia, value)) return;
            Raise(nameof(HasMedia));
            Raise(nameof(HasNext));
            Raise(nameof(HasPrev));
            Raise(nameof(IsAudioPlayback));
            Raise(nameof(IsVideoSurfaceVisible));
        }
    }

    /// <summary>오디오 파일을 재생/로딩 중 — 음표 오버레이를 띄울 조건.</summary>
    public bool IsAudioPlayback =>
        CurrentMedia?.Kind == MediaKind.Audio &&
        State is PlayerState.Playing or PlayerState.Paused or PlayerState.Ready or PlayerState.Loading;

    /// <summary>mpv child hwnd를 보여야 할 조건 — 비디오/이미지일 때만. 오디오는 Hidden.</summary>
    public bool IsVideoSurfaceVisible =>
        CurrentMedia?.Kind is MediaKind.Video or MediaKind.Image &&
        State is PlayerState.Playing or PlayerState.Paused or PlayerState.Ready or PlayerState.Loading;
    public bool HasMedia => CurrentMedia is not null;
    // ⏭/⏮은 자동 진행과 같은 same-kind 규칙을 따른다. 영상 + png + 영상2 폴더에서 영상 보다가
    // ⏭ 누르면 png가 아니라 영상2로. RepeatAll이면 마지막 곡에서도 처음으로 wrap (자동 EOF와 일치).
    public bool HasNext  => CurrentIndex >= 0 &&
                            (NextOfSameKind() is not null ||
                             (Repeat == RepeatMode.RepeatAll && FirstOfSameKind() is { } first && first != CurrentMedia));
    public bool HasPrev  => CurrentIndex >= 0 &&
                            (PrevOfSameKind() is not null ||
                             (Repeat == RepeatMode.RepeatAll && LastOfSameKind() is { } last && last != CurrentMedia));

    public int CurrentIndex
    {
        get
        {
            if (CurrentMedia is null) return -1;
            return _playlistSvc.IndexOf(Playlist, CurrentMedia.FullPath);
        }
    }

    private double _timePos;
    public double TimePos
    {
        get => _timePos;
        set { if (Set(ref _timePos, value)) Raise(nameof(TimePosDisplay)); }
    }
    public string TimePosDisplay => TimeFormat.Seconds(_timePos);

    private double _duration;
    public double Duration
    {
        get => _duration;
        set { if (Set(ref _duration, value)) Raise(nameof(DurationDisplay)); }
    }
    public string DurationDisplay => TimeFormat.Seconds(_duration);

    private bool _isPaused;
    public bool IsPaused
    {
        get => _isPaused;
        set
        {
            if (!Set(ref _isPaused, value)) return;
            if (State is PlayerState.Playing or PlayerState.Paused or PlayerState.Ready)
                State = value ? PlayerState.Paused : PlayerState.Playing;
        }
    }

    private double _volume = 70;
    public double Volume
    {
        get => _volume;
        set
        {
            var v = Math.Clamp(value, 0, 100);
            if (!Set(ref _volume, v)) return;
            Settings.Volume = (int)Math.Round(v);
            _ = _ipc.SetVolume(v);
            // 변경 시 자동으로 mute 해제
            if (v > 0 && IsMuted) IsMuted = false;
        }
    }

    private bool _muted;
    public bool IsMuted
    {
        get => _muted;
        set
        {
            if (!Set(ref _muted, value)) return;
            Settings.Muted = value;
            _ = _ipc.SetMute(value);
        }
    }

    private double _speed = 1.0;
    public double Speed
    {
        get => _speed;
        set
        {
            var v = Math.Clamp(value, 0.25, 4.0);
            if (!Set(ref _speed, v)) return;
            Settings.PlaybackRate = v;
            _ = _ipc.SetSpeed(v);
            Raise(nameof(SpeedDisplay));
        }
    }
    public string SpeedDisplay => _speed == 1.0 ? "1.0x" : _speed.ToString("0.00") + "x";

    /// <summary>
    /// 메뉴 preset / reset 버튼 전용. 일반 Speed setter는 동일값이면 early-return이라
    /// "이미 1.0인데 1.0 다시 click" 같은 케이스에서 IPC가 안 보내져 dead처럼 보임.
    /// 여기서는 항상 IPC SetSpeed 보내고 SpeedDisplay raise.
    /// </summary>
    private void SetSpeedForce(double value)
    {
        var v = Math.Round(Math.Clamp(value, 0.25, 4.0), 2);
        _speed = v;
        Settings.PlaybackRate = v;
        _ = _ipc.SetSpeed(v);
        Raise(nameof(Speed));
        Raise(nameof(SpeedDisplay));
    }

    private bool _isFullscreen;
    public bool IsFullscreen { get => _isFullscreen; set => Set(ref _isFullscreen, value); }

    // 자동 update 후보. UpdaterService.CheckAsync가 새 버전 발견 시 채워줌.
    // UI: TopBar에 update button visible 여부 binding.
    private Velopack.UpdateInfo? _pendingUpdate;
    private bool _updateReadyToApply;
    private bool _updatePortable;
    private bool _applyingUpdate;
    private bool _isUpdateAvailable;
    public bool IsUpdateAvailable
    {
        get => _isUpdateAvailable;
        set { if (Set(ref _isUpdateAvailable, value)) { Raise(nameof(UpdateTooltip)); ApplyUpdateCommand.RaiseCanExecuteChanged(); } }
    }
    private string? _updateNewVersion;
    public string? UpdateNewVersion
    {
        get => _updateNewVersion;
        set { if (Set(ref _updateNewVersion, value)) Raise(nameof(UpdateTooltip)); }
    }
    public string UpdateTooltip => _isUpdateAvailable && _updateNewVersion is not null
        ? (_updatePortable
            ? LF("UpdatePortableAvailable", _updateNewVersion)
            : _updateReadyToApply
                ? LF("UpdateReady", _updateNewVersion)
                : LF("UpdateAvailable", _updateNewVersion))
        : L("UpdateChecking");
    /// <summary>UpdaterService 결과를 받아서 state 갱신 + UI에 button 표시 trigger.</summary>
    public void SetPendingUpdate(
        string newVersion,
        Velopack.UpdateInfo? info,
        bool readyToApply = false,
        bool portable = false)
    {
        OnUi(() =>
        {
            _pendingUpdate = info;
            _updateReadyToApply = readyToApply;
            _updatePortable = portable;
            UpdateNewVersion = newVersion;
            Raise(nameof(UpdateTooltip));
            IsUpdateAvailable = true;
        });
    }

    private async Task ApplyPendingUpdateAsync()
    {
        if (_applyingUpdate) return;
        _applyingUpdate = true;
        ApplyUpdateCommand.RaiseCanExecuteChanged();
        try
        {
            var ok = await Services.UpdaterService.ApplyAsync(_pendingUpdate).ConfigureAwait(false);
            if (!ok)
                OnUi(() => Toast?.Invoke(L("UpdateApplyFailed")));
        }
        finally
        {
            _applyingUpdate = false;
            ApplyUpdateCommand.RaiseCanExecuteChanged();
        }
    }

    private bool _isAlwaysOnTop;
    public bool IsAlwaysOnTop
    {
        get => _isAlwaysOnTop;
        set { if (Set(ref _isAlwaysOnTop, value)) Settings.AlwaysOnTop = value; }
    }

    private bool _isPlaylistOpen = true;
    public bool IsPlaylistOpen
    {
        get => _isPlaylistOpen;
        set
        {
            if (!Set(ref _isPlaylistOpen, value)) return;
            Settings.PlaylistPanelEnabled = value;
            Raise(nameof(PlaylistColumnWidth));
        }
    }
    public System.Windows.GridLength PlaylistColumnWidth =>
        _isPlaylistOpen ? new System.Windows.GridLength(320) : new System.Windows.GridLength(0);

    // ────────────────────────────────────────────────────────
    // Repeat / Shuffle
    // ────────────────────────────────────────────────────────
    private RepeatMode _repeat = RepeatMode.None;
    public RepeatMode Repeat
    {
        get => _repeat;
        set
        {
            if (!Set(ref _repeat, value)) return;
            Settings.RepeatMode = (int)value;
            Raise(nameof(RepeatGlyph));
            Raise(nameof(RepeatTooltip));
            Raise(nameof(IsRepeatActive));
            Raise(nameof(IsRepeatNone));
            Raise(nameof(IsRepeatOne));
            Raise(nameof(IsRepeatAll));
            // RepeatAll 켜면 마지막 곡에서도 ⏭ 활성. 끄면 비활성. HasNext/HasPrev 다시 평가.
            Raise(nameof(HasNext));
            Raise(nameof(HasPrev));
            NextCommand.RaiseCanExecuteChanged();
            PrevCommand.RaiseCanExecuteChanged();
            Services.AppLog.Info($"Repeat set -> {value}");
            _ = SyncLoopFileToMpv();
        }
    }

    /// <summary>
    /// mpv loop-file을 현재 Repeat 모드와 강제 동기화 + 즉시 동작 변화도 시도.
    ///   None       → loop-file=no.
    ///   RepeatAll  → loop-file=no (전체 반복은 우리가 OnEndFile에서 wrap).
    ///   RepeatOne  → loop-file=inf (mpv가 자체 loop).
    /// CommandAsync로 응답 확인. 응답 받은 뒤 mpv 쪽에서 실제 값 읽어 한 번 더 검증.
    /// </summary>
    private async Task SyncLoopFileToMpv()
    {
        var val = _repeat == RepeatMode.RepeatOne ? "inf" : "no";
        if (!_ipc.IsConnected) return;
        try
        {
            await _ipc.CommandAsync("set_property", "loop-file", val);
            Services.AppLog.Info($"  mpv loop-file <= {val} OK");

            // 검증: mpv가 실제로 받았는지 readback
            try
            {
                var got = await _ipc.CommandAsync("get_property", "loop-file");
                var gotStr = got.ValueKind switch
                {
                    System.Text.Json.JsonValueKind.String => got.GetString(),
                    System.Text.Json.JsonValueKind.Number => got.GetRawText(),
                    System.Text.Json.JsonValueKind.False  => "no",
                    _ => got.ToString()
                };
                Services.AppLog.Info($"  mpv loop-file readback = {gotStr}");
            }
            catch { /* 검증 실패는 무해 */ }
        }
        catch (Exception ex)
        {
            Services.AppLog.Warn($"  mpv loop-file <= {val} FAILED: {ex.Message}");
        }
    }
    public bool IsRepeatActive => _repeat != RepeatMode.None;
    public bool IsRepeatNone   => _repeat == RepeatMode.None;
    public bool IsRepeatOne    => _repeat == RepeatMode.RepeatOne;
    public bool IsRepeatAll    => _repeat == RepeatMode.RepeatAll;
    /// <summary>
    /// Segoe MDL2 Assets 글리프 (Fluent Icons도 동일 코드).
    ///   None / RepeatAll →  (RepeatAll: 좌→우 루프 화살표)
    ///   RepeatOne        →  (RepeatOne: 루프 + "1")
    /// 색상으로 None ↔ Active를 구분. \u-escape로 BAML 인코딩 손상 위험 차단.
    /// </summary>
    public string RepeatGlyph => _repeat switch
    {
        RepeatMode.RepeatOne => "",   // Repeat1
        _ => ""                        // RepeatAll
    };
    public string RepeatTooltip => _repeat switch
    {
        RepeatMode.None      => L("RepeatTooltipNone"),
        RepeatMode.RepeatAll => L("RepeatTooltipAll"),
        RepeatMode.RepeatOne => L("RepeatTooltipOne"),
        _ => ""
    };

    private bool _shuffle;
    public bool Shuffle
    {
        get => _shuffle;
        set
        {
            if (!Set(ref _shuffle, value)) return;
            Settings.Shuffle = value;
            Raise(nameof(ShuffleTooltip));
            Services.AppLog.Info($"Shuffle set -> {value}");
        }
    }
    public string ShuffleTooltip => _shuffle
        ? L("ShuffleOn")
        : L("ShuffleOff");

    // ============================================================
    // commands
    // ============================================================

    public RelayCommand PlayPauseCommand { get; }
    public RelayCommand NextCommand { get; }
    public RelayCommand PrevCommand { get; }
    public RelayCommand OpenCommand { get; }
    public RelayCommand MuteCommand { get; }
    public RelayCommand FullscreenCommand { get; }
    public RelayCommand ExitFullscreenCommand { get; }
    public RelayCommand ScreenshotCommand { get; }
    public RelayCommand AlwaysOnTopCommand { get; }
    public RelayCommand TogglePlaylistCommand { get; }
    public RelayCommand CycleRepeatCommand { get; }
    public RelayCommand SetRepeatNoneCommand { get; }
    public RelayCommand SetRepeatAllCommand  { get; }
    public RelayCommand SetRepeatOneCommand  { get; }
    public RelayCommand ToggleShuffleCommand { get; }
    public RelayCommand Seek5ForwardCommand { get; }
    public RelayCommand Seek5BackwardCommand { get; }
    public RelayCommand Seek30ForwardCommand { get; }
    public RelayCommand Seek30BackwardCommand { get; }
    public RelayCommand VolumeUpCommand { get; }
    public RelayCommand VolumeDownCommand { get; }
    public RelayCommand FrameStepCommand { get; }
    public RelayCommand FrameBackCommand { get; }
    public RelayCommand IncreaseSpeedCommand { get; }
    public RelayCommand DecreaseSpeedCommand { get; }
    public RelayCommand ResetSpeedCommand { get; }
    public RelayCommand SetSpeedCommand { get; }
    public RelayCommand SetTrimInCommand { get; }
    public RelayCommand SetTrimOutCommand { get; }
    public RelayCommand ClearTrimCommand { get; }
    public RelayCommand ExecuteTrimCommand { get; }
    public RelayCommand ToggleTrimModeCommand { get; }
    public RelayCommand CancelTrimModeCommand { get; }
    public RelayCommand OpenFolderCommand { get; }

    // 편집 모드 — 가위 버튼 두 번 누르기 사이의 상태. SeekBar 위 IN/OUT 핸들 드래그.
    private bool _isTrimMode;
    public bool IsTrimMode
    {
        get => _isTrimMode;
        set => Set(ref _isTrimMode, value);
    }

    // ─── Trim state ────────────────────────────────────────────────────
    // IN/OUT 지점(초). null = 미설정. 둘 다 설정되고 OUT>IN이면 Execute 가능.
    private double? _trimInSec;
    public double? TrimInSec
    {
        get => _trimInSec;
        set
        {
            if (!Set(ref _trimInSec, value)) return;
            Raise(nameof(TrimRangeDisplay)); Raise(nameof(HasTrimRange));
            if (IsTrimMode) ApplyTrimLoop();
        }
    }
    private double? _trimOutSec;
    public double? TrimOutSec
    {
        get => _trimOutSec;
        set
        {
            if (!Set(ref _trimOutSec, value)) return;
            Raise(nameof(TrimRangeDisplay)); Raise(nameof(HasTrimRange));
            if (IsTrimMode) ApplyTrimLoop();
        }
    }

    /// <summary>편집 모드 진입 / IN-OUT 변경 시 mpv에 ab-loop 적용.
    /// mpv가 자동으로 a~b 구간 재생 후 a로 점프 → 사용자가 그 구간만 미리듣기.</summary>
    private void ApplyTrimLoop()
    {
        if (!HasTrimRange) return;
        _ = _ipc.SetAbLoop(_trimInSec!.Value, _trimOutSec!.Value);
    }
    public bool HasTrimRange =>
        _trimInSec is double i && _trimOutSec is double o && o > i;
    public string TrimRangeDisplay => HasTrimRange
        ? $"{Helpers.TimeFormat.Seconds(_trimInSec!.Value)} → {Helpers.TimeFormat.Seconds(_trimOutSec!.Value)}"
        : "";

    private bool _trimBusy;
    // 버튼 IsEnabled — HasTrimRange는 검사 안 함 (disabled되면 클릭 시 toast 안내 못 함).
    // 사용자가 가위 버튼 그냥 누르면 ExecuteTrimAsync 안에서 "IN/OUT 먼저" 안내 toast.
    private bool CanExecuteTrim() =>
        !_trimBusy
        && CurrentMedia is not null
        && CurrentMedia.Kind != MediaKind.Image;

    private async Task<bool> ExecuteTrimAsync()
    {
        if (_trimBusy) return false;
        if (CurrentMedia is null)
        { Toast?.Invoke(L("NoPlayingMedia")); return false; }
        if (CurrentMedia.Kind == MediaKind.Image)
        { Toast?.Invoke(L("ImageCannotTrim")); return false; }
        if (!HasTrimRange)
        { Toast?.Invoke(L("TrimNeedInOut")); return false; }

        var src = CurrentMedia!.FullPath;
        var inS = _trimInSec!.Value;
        var outS = _trimOutSec!.Value;
        var srcDir  = System.IO.Path.GetDirectoryName(src) ?? Environment.CurrentDirectory;
        var srcName = System.IO.Path.GetFileNameWithoutExtension(src);
        var ext = System.IO.Path.GetExtension(src);
        var defaultName = $"{srcName}_clip{ext}";

        // 사용자가 직접 저장 위치 + 이름 지정
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = L("TrimSaveDialogTitle"),
            FileName = defaultName,
            InitialDirectory = srcDir,
            Filter = LF("SameFormatFilter", ext),
            AddExtension = true,
            DefaultExt = ext,
            OverwritePrompt = true,
        };
        if (dlg.ShowDialog() != true)
        {
            // 사용자 취소 — 편집 모드 유지, 아무것도 변경 안 함
            return false;
        }
        var outputPath = dlg.FileName;

        _trimBusy = true;
        ExecuteTrimCommand.RaiseCanExecuteChanged();
        try
        {
            Toast?.Invoke(L("TrimInProgress"));
            var res = await Services.TrimService.TrimAsync(src, inS, outS, outputPath).ConfigureAwait(true);
            if (res.Success && res.OutputPath is not null)
            {
                var name = System.IO.Path.GetFileName(res.OutputPath);
                Toast?.Invoke(LF("TrimSaved", name));
                Services.AppLog.Info($"Trim saved: {res.OutputPath}");
                return true;
            }
            else
            {
                Toast?.Invoke(LF("TrimFailed", res.Error));
                return false;
            }
        }
        finally
        {
            _trimBusy = false;
            ExecuteTrimCommand.RaiseCanExecuteChanged();
        }
    }
    public RelayCommand ApplyUpdateCommand { get; }
    public RelayCommand CycleSubtitleCommand { get; }
    public RelayCommand ToggleSubtitleVisibilityCommand { get; }
    public RelayCommand CycleAudioCommand { get; }

    private void TogglePlayPause()
    {
        if (CurrentMedia is null) { OpenDialog(); return; }
        if (CurrentMedia.Kind == MediaKind.Image) return; // 이미지는 토글 무의미

        // 영상 끝나서 멈춘 상태 → 처음부터 다시 재생 (사용자가 Space 누른 의도)
        if (IsAtEnd)
        {
            _ = _ipc.SeekAbsolute(0);
            _ = _ipc.SetPause(false);
            IsAtEnd = false;
            return;
        }

        // 재생 실패 상태에서 Space → 같은 파일 다시 시도
        if (State == PlayerState.Failed)
        {
            PlayMedia(CurrentMedia);
            return;
        }

        _ = _ipc.SetPause(!IsPaused);
    }

    private void OpenDialog()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = L("OpenMediaDialogTitle"),
            CheckFileExists = true,
            Multiselect = false,
            Filter = BuildFileFilter(),
            InitialDirectory = Settings.LastOpenedFolder ?? ""
        };
        if (dlg.ShowDialog() == true)
            OpenPath(dlg.FileName);
    }

    private void OpenFolderDialog()
    {
        try
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog
            {
                Title = L("OpenMediaFolderDialogTitle"),
                InitialDirectory = Settings.LastOpenedFolder ?? ""
            };
            if (dlg.ShowDialog() != true) return;

            // 자연 정렬로 폴더 안 첫 지원 미디어 파일 찾기
            var first = System.IO.Directory.EnumerateFiles(dlg.FolderName)
                .Where(MediaKindExtensions.IsSupported)
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            if (first is null)
                Toast?.Invoke(L("NoPlayableMediaInFolder"));
            else
                OpenPath(first);
        }
        catch (Exception ex)
        {
            Services.AppLog.Error("OpenFolderDialog", ex);
            Toast?.Invoke(LF("OpenFolderFailed", ex.Message));
        }
    }

    private static string BuildFileFilter()
    {
        var all = MediaKindExtensions.AllSupportedExtensions()
            .Select(e => "*" + e).Distinct().OrderBy(s => s);
        var allJoined = string.Join(";", all);
        return LF("AllSupportedMediaFilter", allJoined);
    }

    // ============================================================
    // open / playlist
    // ============================================================

    public void OpenPath(string path)
    {
        Services.AppLog.Info($"OpenPath: '{path}' state={State} ipcConn={_ipc.IsConnected}");
        if (string.IsNullOrWhiteSpace(path)) { Services.AppLog.Warn("OpenPath: empty path"); return; }
        if (!File.Exists(path))
        {
            State = PlayerState.Failed;
            StatusMessage = LF("FileNotFound", path);
            return;
        }
        if (!MediaKindExtensions.IsSupported(path))
        {
            State = PlayerState.Failed;
            StatusMessage = LF("UnsupportedFormat", Path.GetExtension(path));
            return;
        }
        if (!_ipc.IsConnected)
        {
            State = PlayerState.Failed;
            StatusMessage = L("MpvDisconnectedRestart");
            return;
        }

        var folder = Path.GetDirectoryName(path);
        Settings.LastOpenedFolder = folder;
        TouchRecent(path);

        var list = _playlistSvc.BuildFromFile(path);
        Playlist.Clear();
        foreach (var m in list) Playlist.Add(m);
        var media = Playlist.FirstOrDefault(m =>
            string.Equals(m.FullPath, path, StringComparison.OrdinalIgnoreCase));
        if (media is null && Playlist.Count > 0) media = Playlist[0];

        FolderDisplay = folder ?? "";
        PlayMedia(media);
    }

    public void PlayMedia(MediaItem? media)
    {
        Services.AppLog.Info($"PlayMedia: {media?.FileName ?? "(null)"}");
        if (media is null) return;
        foreach (var m in Playlist) m.IsPlaying = false;
        media.IsPlaying = true;
        media.HasError = false;
        media.ErrorMessage = null;
        CurrentMedia = media;
        FileNameDisplay = media.FileName;
        State = PlayerState.Loading;
        StatusMessage = L("LoadingStatus");
        IsAtEnd = false;
        _ = _ipc.LoadFile(media.FullPath);
        _ = _ipc.SetPause(false);
        // mpv는 loop-file을 file 간에 유지. 새 파일 로드마다 우리 Repeat 모드와 강제 sync.
        _ = SyncLoopFileToMpv();
        ApplyVisualizer(media.Kind);

        if (_isPaused) IsPaused = false;
        NextCommand.RaiseCanExecuteChanged();
        PrevCommand.RaiseCanExecuteChanged();
    }

    private void ApplyVisualizer(MediaKind kind)
    {
        // mpv 비주얼라이저는 사용자 환경에서 안정성/시각 둘 다 별로 — 빼고 WPF 오버레이로 처리.
        // audio 파일은 mpv에서 video 출력 없이 재생, 우리 영상 host는 Hidden, 그 자리에 음표 아이콘.
        _ = _ipc.SendAsync("set_property", "lavfi-complex", "");
    }

    private void Next()
    {
        if (CurrentMedia is null) return;
        var target = NextOfSameKind() ??
                     (Repeat == RepeatMode.RepeatAll ? FirstOfSameKind() : null);
        if (target is not null && target != CurrentMedia) PlayMedia(target);
    }

    private void Prev()
    {
        if (CurrentMedia is null) return;
        var target = PrevOfSameKind() ??
                     (Repeat == RepeatMode.RepeatAll ? LastOfSameKind() : null);
        if (target is not null && target != CurrentMedia) PlayMedia(target);
    }

    private void SeekBy(double delta)
    {
        if (CurrentMedia is null || CurrentMedia.Kind == MediaKind.Image) return;
        // EOF 상태에서 ← 누르면: 끝에서 delta초 뒤로 + 자동 재생
        if (IsAtEnd)
        {
            var target = Math.Max(0, Duration + delta);
            _ = _ipc.SeekAbsolute(target);
            _ = _ipc.SetPause(false);
            IsAtEnd = false;
            return;
        }
        _ = _ipc.SeekRelative(delta);
    }

    private async void TakeScreenshot()
    {
        Services.AppLog.Info($"TakeScreenshot invoked: media={CurrentMedia?.FileName ?? "(null)"} ipc={_ipc.IsConnected}");
        if (CurrentMedia is null)
        {
            Toast?.Invoke(L("NoScreenshotWithoutMedia"));
            return;
        }
        if (!_ipc.IsConnected)
        {
            Toast?.Invoke(L("MpvDisconnectedToast"));
            return;
        }
        if (CurrentMedia.Kind == MediaKind.Audio)
        {
            Toast?.Invoke(L("AudioCannotScreenshot"));
            return;
        }
        try
        {
            // 사용자 지정 폴더 우선, 없으면 기본 Pictures\Deno Video Player\
            var dir = !string.IsNullOrWhiteSpace(Settings.ScreenshotFolder)
                ? Settings.ScreenshotFolder
                : Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                    "Deno Video Player");
            Directory.CreateDirectory(dir);
            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var raw  = Path.GetFileNameWithoutExtension(CurrentMedia.FileName);
            // mpv가 일부 유니코드/특수문자 경로에서 실패하는 경우가 있어 file-name 안전화
            var safe = string.Concat(raw.Where(c => !Path.GetInvalidFileNameChars().Contains(c)));
            if (string.IsNullOrWhiteSpace(safe)) safe = "screenshot";
            var path = Path.Combine(dir, $"{safe}_{stamp}.png");
            Services.AppLog.Info($"Screenshot -> {path}");
            // mpv 응답 받아서 실패면 정확히 알림. 성공이면 실제 파일 생성도 확인.
            var ok = await _ipc.ScreenshotChecked(path).ConfigureAwait(false);
            OnUi(() =>
            {
                if (ok && File.Exists(path))
                    Toast?.Invoke(LF("ScreenshotSaved", Path.GetFileName(path)));
                else
                    Toast?.Invoke(L("ScreenshotNoResponse"));
            });
        }
        catch (Exception ex)
        {
            Services.AppLog.Error("Screenshot", ex);
            OnUi(() => Toast?.Invoke(LF("ScreenshotFailed", ex.Message)));
        }
    }

    // ============================================================
    // mpv events
    // ============================================================

    private bool _seeking;
    public bool Seeking { get => _seeking; set => Set(ref _seeking, value); }

    /// <summary>mpv 'eof-reached' = true. EOF 멈춤 상태에서 다음 액션 처리에 사용.</summary>
    private bool _isAtEnd;
    public bool IsAtEnd
    {
        get => _isAtEnd;
        set => Set(ref _isAtEnd, value);
    }

    public void BeginSeek() => Seeking = true;

    /// <summary>
    /// 사용자가 새 위치로 seek 확정. mpv가 seek 명령을 적용하기 직전까지 이전 위치의
    /// time-pos를 마지막으로 한 번 더 emit하는 경우가 있어, Seeking=true 상태를 짧게
    /// 더 유지해 그 stale 값이 thumb을 원래 자리로 끌어오는 'snap back'을 방지한다.
    /// </summary>
    public async void EndSeek(double seconds)
    {
        _ = _ipc.SeekAbsolute(seconds);
        if (IsAtEnd)
        {
            _ = _ipc.SetPause(false);
            IsAtEnd = false;
        }
        // mpv가 새 위치를 반영한 time-pos를 보내기 충분한 시간 동안 Seeking 유지
        try { await Task.Delay(250).ConfigureAwait(false); }
        catch { /* 무해 */ }
        OnUi(() => Seeking = false);
    }

    /// <summary>드래그 중 live preview seek. UI(TimePos)를 즉시 갱신하고 mpv에도 명령.
    /// 일시정지 상태에서는 mpv가 time-pos event를 느리게 보내거나 한 번만 보내서
    /// UI 반영이 늦음. 즉시 set으로 SeekBar Value/시간 표시가 바로 따라옴.</summary>
    public void LiveSeek(double seconds)
    {
        TimePos = seconds;
        _ = _ipc.SeekAbsolute(seconds);
    }

    // mpv 이벤트가 매 frame (60Hz+) 옴 → UI 스레드 dispatch 폭주를 막아 키 입력 응답성 확보
    private DateTime _lastTimePosAt;

    private void OnMpvPropertyChanged(string name, JsonElement? value)
    {
        // throttle을 dispatcher 진입 전에 — 큐 자체를 비움
        if (name == "time-pos")
        {
            var now = DateTime.UtcNow;
            if (now - _lastTimePosAt < TimeSpan.FromMilliseconds(120)) return;
            _lastTimePosAt = now;
        }

        OnUi(() =>
        {
            switch (name)
            {
                case "time-pos":
                    if (!Seeking && TryGetDouble(value, out var t)) TimePos = t;
                    break;
                case "duration":
                    if (TryGetDouble(value, out var d)) Duration = d;
                    break;
                case "pause":
                    if (TryGetBool(value, out var p)) IsPaused = p;
                    break;
                case "volume":
                    if (TryGetDouble(value, out var v))
                    {
                        // mpv → UI 동기화 시 setter 재진입 막기 위해 직접 백킹 변경
                        if (Math.Abs(_volume - v) > 0.5)
                        {
                            _volume = Math.Clamp(v, 0, 100);
                            Settings.Volume = (int)Math.Round(_volume);
                            Raise(nameof(Volume));
                        }
                    }
                    break;
                case "mute":
                    if (TryGetBool(value, out var mt) && _muted != mt)
                    {
                        _muted = mt; Settings.Muted = mt; Raise(nameof(IsMuted));
                    }
                    break;
                case "speed":
                    if (TryGetDouble(value, out var sp) && Math.Abs(_speed - sp) > 0.001)
                    {
                        _speed = sp; Settings.PlaybackRate = sp;
                        Raise(nameof(Speed)); Raise(nameof(SpeedDisplay));
                    }
                    break;
                case "filename":
                    if (value is { ValueKind: JsonValueKind.String } se)
                        FileNameDisplay = se.GetString() ?? FileNameDisplay;
                    break;
                case "eof-reached":
                    if (TryGetBool(value, out var eof)) IsAtEnd = eof;
                    break;
                // mouse-pos observe 폐기 — MainWindow의 GetCursorPos polling이 mouse 활동
                // 감지 + hot zone trigger 둘 다 담당. mpv 좌표계 신뢰 못 함.
            }
        });
    }

    private void OnFileLoaded()
    {
        Services.AppLog.Info($"OnFileLoaded: {CurrentMedia?.FileName}");
        OnUi(() =>
        {
            State = IsPaused ? PlayerState.Paused : PlayerState.Playing;
            IsAtEnd = false;
            StatusMessage = "";
        });
        // EOF 후 자동 next 시: mpv가 keep-open=yes로 pause=true 상태일 수 있음.
        // 새 파일 로드되면 무조건 재생되도록 한 번 더 보장.
        _ = _ipc.SetPause(false);
    }

    private static readonly Random _rng = new();

    private void OnEndFile(string? reason)
    {
        Services.AppLog.Info($"OnEndFile reason={reason} repeat={Repeat} shuffle={Shuffle} hasNext={HasNext} idx={CurrentIndex} count={Playlist.Count}");
        OnUi(() =>
        {
            var progress = reason is "eof" or "redirect" or "unknown" or null;
            if (progress)
            {
                if (Repeat == RepeatMode.RepeatOne && CurrentMedia is not null)
                {
                    Services.AppLog.Info("  -> RepeatOne replay");
                    PlayMedia(CurrentMedia);
                    return;
                }

                if (Shuffle)
                {
                    var rnd = PickRandomOfSameKind();
                    if (rnd is not null)
                    {
                        Services.AppLog.Info($"  -> Shuffle next (sameKind) -> {rnd.FileName}");
                        PlayMedia(rnd);
                        return;
                    }
                }

                // 자동 next는 같은 종류(영상↔영상, 음악↔음악)만 따라간다.
                // ComfyUI 같은 폴더의 음악 옆에 .png 미리보기가 섞여있어도 음악 → 다음 음악으로 진행.
                var nextSame = NextOfSameKind();
                if (nextSame is not null)
                {
                    Services.AppLog.Info($"  -> Next (sameKind) -> {nextSame.FileName}");
                    PlayMedia(nextSame);
                    return;
                }

                if (Repeat == RepeatMode.RepeatAll)
                {
                    var firstSame = FirstOfSameKind();
                    if (firstSame is not null)
                    {
                        Services.AppLog.Info($"  -> RepeatAll wrap (sameKind) -> {firstSame.FileName}");
                        PlayMedia(firstSame);
                        return;
                    }
                }

                Services.AppLog.Info("  -> stop (no more of same kind)");
            }
            else if (reason == "error")
            {
                if (CurrentMedia is not null)
                {
                    CurrentMedia.HasError = true;
                    CurrentMedia.ErrorMessage = L("PlaybackFailedShort");
                }
                State = PlayerState.Failed;
                StatusMessage = L("CannotPlayThisFile");
            }
        });
    }

    /// <summary>현재 항목 다음 인덱스부터 같은 kind인 첫 항목.</summary>
    private MediaItem? NextOfSameKind()
    {
        if (CurrentMedia is null) return null;
        var kind = CurrentMedia.Kind;
        var idx = CurrentIndex;
        for (var i = idx + 1; i < Playlist.Count; i++)
            if (Playlist[i].Kind == kind) return Playlist[i];
        return null;
    }

    /// <summary>현재 항목 이전 인덱스부터 거꾸로 가서 같은 kind인 첫 항목.</summary>
    private MediaItem? PrevOfSameKind()
    {
        if (CurrentMedia is null) return null;
        var kind = CurrentMedia.Kind;
        var idx = CurrentIndex;
        if (idx < 0) return null;
        for (var i = idx - 1; i >= 0; i--)
            if (Playlist[i].Kind == kind) return Playlist[i];
        return null;
    }

    /// <summary>재생목록 처음부터 같은 kind인 첫 항목 (RepeatAll wrap용).</summary>
    private MediaItem? FirstOfSameKind()
    {
        if (CurrentMedia is null) return null;
        var kind = CurrentMedia.Kind;
        return Playlist.FirstOrDefault(m => m.Kind == kind);
    }

    /// <summary>재생목록 마지막부터 거꾸로 가서 같은 kind인 첫 항목 (RepeatAll wrap-prev용).</summary>
    private MediaItem? LastOfSameKind()
    {
        if (CurrentMedia is null) return null;
        var kind = CurrentMedia.Kind;
        for (var i = Playlist.Count - 1; i >= 0; i--)
            if (Playlist[i].Kind == kind) return Playlist[i];
        return null;
    }

    /// <summary>같은 kind 중 현재 곡 외에서 랜덤 하나 (Shuffle 자동 next).</summary>
    private void TouchRecent(string path)
    {
        // 같은 path 있으면 제거 후 맨 위에 다시 → 최신이 0번
        for (var i = Recents.Count - 1; i >= 0; i--)
            if (string.Equals(Recents[i].FullPath, path, StringComparison.OrdinalIgnoreCase))
                Recents.RemoveAt(i);
        Recents.Insert(0, new RecentItem(path));
        while (Recents.Count > MaxRecents) Recents.RemoveAt(Recents.Count - 1);
        Settings.RecentFiles = Recents.Select(r => r.FullPath).ToList();
    }

    private MediaItem? PickRandomOfSameKind()
    {
        if (CurrentMedia is null) return null;
        var kind = CurrentMedia.Kind;
        var pool = Playlist.Where(m => m.Kind == kind && m != CurrentMedia).ToList();
        if (pool.Count == 0) return null;
        return pool[_rng.Next(pool.Count)];
    }

    // ============================================================
    // drag / drop
    // ============================================================

    public bool TryOpenDroppedFiles(string[] files)
    {
        if (files is null || files.Length == 0) return false;
        // 1. 미디어 + 자막 분리. 미디어가 있으면 그걸 열고, 자막만 있으면 현재 영상에 add-sub.
        var media = files.FirstOrDefault(f => File.Exists(f) && MediaKindExtensions.IsSupported(f));
        var subs  = files.Where(f => File.Exists(f) && MediaKindExtensions.IsSubtitle(f)).ToList();

        // 폴더 드롭: 첫 미디어 파일 사용
        if (media is null)
        {
            foreach (var f in files)
            {
                if (Directory.Exists(f))
                {
                    media = Directory.EnumerateFiles(f)
                        .Where(MediaKindExtensions.IsSupported)
                        .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                        .FirstOrDefault();
                    if (media is not null) break;
                }
            }
        }

        if (media is not null)
        {
            OpenPath(media);
            // 자막도 같이 떨어졌으면 add-sub. mpv는 동일 파일명+다른 ext도 자동 로드하지만 명시적 add가 신뢰성↑
            foreach (var s in subs)
            {
                _ = _ipc.LoadSubtitle(s);
            }
            return true;
        }

        // 자막만 드롭됐고 현재 영상이 있으면 add-sub
        if (subs.Count > 0 && _ipc.IsConnected)
        {
            if (CurrentMedia is null)
            {
                Toast?.Invoke(L("NoVideoForSubtitle"));
                return true;
            }
            if (CurrentMedia.Kind != MediaKind.Video)
            {
                Toast?.Invoke(L("SubtitleOnlyVideo"));
                return true;
            }
            foreach (var s in subs)
            {
                _ = _ipc.LoadSubtitle(s);
            }
            Toast?.Invoke(LF("SubtitlesAdded", subs.Count));
            return true;
        }

        return false;
    }

    // ============================================================
    // helpers
    // ============================================================

    private static bool TryGetDouble(JsonElement? value, out double result)
    {
        result = 0;
        if (value is null) return false;
        var v = value.Value;
        return v.ValueKind switch
        {
            JsonValueKind.Number => v.TryGetDouble(out result),
            JsonValueKind.Null => false,
            _ => false
        };
    }
    private static bool TryGetBool(JsonElement? value, out bool result)
    {
        result = false;
        if (value is null) return false;
        var v = value.Value;
        if (v.ValueKind is JsonValueKind.True) { result = true; return true; }
        if (v.ValueKind is JsonValueKind.False) { result = false; return true; }
        return false;
    }

    private static string L(string key) => LocalizationService.T(key);
    private static string LF(string key, params object?[] args) => LocalizationService.F(key, args);

    private void OnLanguageChanged()
    {
        OnUi(() =>
        {
            Raise(nameof(UpdateTooltip));
            Raise(nameof(RepeatTooltip));
            Raise(nameof(ShuffleTooltip));
        });
    }

    private void OnUi(Action a)
    {
        if (_ui.CheckAccess()) a();
        else _ui.BeginInvoke(a);
    }

    public void PersistSettings(double windowW, double windowH, double? left, double? top, bool maximized)
    {
        Settings.WindowWidth = windowW;
        Settings.WindowHeight = windowH;
        Settings.WindowLeft = left;
        Settings.WindowTop = top;
        Settings.WindowMaximized = maximized;
        _settingsSvc.Save(Settings);
    }

    /// <summary>
    /// 사용자 명시 액션(Recent 항목 제거/모두 비우기) 직후 디스크 즉시 반영.
    /// Volume/Speed 같은 자주 변하는 값은 close 시 batch save로 두고, 사용자 인지 액션만 즉시.
    /// </summary>
    public void SaveSettingsNow() => _settingsSvc.Save(Settings);

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Raise([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    private bool Set<T>(ref T field, T value, [CallerMemberName] string? n = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value; Raise(n);
        return true;
    }

    public void Dispose()
    {
        LocalizationService.LanguageChanged -= OnLanguageChanged;
        _ipc.Dispose();
    }
}

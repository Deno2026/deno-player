using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using DenoPlayer.Helpers;
using DenoPlayer.Models;
using DenoPlayer.Services;

namespace DenoPlayer.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly MpvProcessService _mpvProc;
    private readonly MpvIpcClient _ipc = new();
    private readonly PlaylistService _playlistSvc = new();
    private readonly SettingsService _settingsSvc = new();
    private readonly System.Windows.Threading.Dispatcher _ui;

    public AppSettings Settings { get; private set; }
    public ObservableCollection<MediaItem> Playlist { get; } = new();

    /// <summary>mpv가 보고하는 마우스 활동(영상 hwnd 위는 WPF가 못 잡음 — IPC로 대체)</summary>
    public event Action? MouseActivity;
    public event Action<double, double>? MpvMousePos;

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

        PlayPauseCommand = new RelayCommand(TogglePlayPause);
        NextCommand      = new RelayCommand(Next, () => HasNext);
        PrevCommand      = new RelayCommand(Prev, () => HasPrev);
        OpenCommand      = new RelayCommand(OpenDialog);
        MuteCommand      = new RelayCommand(() => IsMuted = !IsMuted);
        FullscreenCommand= new RelayCommand(() => IsFullscreen = !IsFullscreen);
        ExitFullscreenCommand = new RelayCommand(() => { if (IsFullscreen) IsFullscreen = false; });
        ScreenshotCommand= new RelayCommand(TakeScreenshot);
        AlwaysOnTopCommand=new RelayCommand(() => IsAlwaysOnTop = !IsAlwaysOnTop);
        TogglePlaylistCommand = new RelayCommand(() => IsPlaylistOpen = !IsPlaylistOpen);
        CycleRepeatCommand = new RelayCommand(() => Repeat = Repeat switch
        {
            RepeatMode.None      => RepeatMode.RepeatAll,
            RepeatMode.RepeatAll => RepeatMode.RepeatOne,
            _                    => RepeatMode.None
        });
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
        ResetSpeedCommand    = new RelayCommand(() => Speed = 1.0);

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
            StatusMessage = "mpv IPC 연결 실패";
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
            await _ipc.ObserveProperty("mouse-pos");
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
        CurrentMedia?.Kind != MediaKind.Audio &&
        State is PlayerState.Playing or PlayerState.Paused or PlayerState.Ready or PlayerState.Loading;
    public bool HasMedia => CurrentMedia is not null;
    public bool HasNext  => CurrentIndex >= 0 && CurrentIndex < Playlist.Count - 1;
    public bool HasPrev  => CurrentIndex > 0;

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

    private bool _isFullscreen;
    public bool IsFullscreen { get => _isFullscreen; set => Set(ref _isFullscreen, value); }

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
            // mpv는 단일 곡 반복은 loop-file=inf로 자체 처리, 전체 반복/none은 우리가 OnEndFile에서
            _ = _ipc.SendAsync("set_property", "loop-file", value == RepeatMode.RepeatOne ? "inf" : "no");
        }
    }
    public bool IsRepeatActive => _repeat != RepeatMode.None;
    public string RepeatGlyph => _repeat switch
    {
        RepeatMode.RepeatOne => "",   // Repeat1
        _ => ""                        // RepeatAll
    };
    public string RepeatTooltip => _repeat switch
    {
        RepeatMode.None      => "반복 끔 (클릭: 전체 반복)",
        RepeatMode.RepeatAll => "전체 반복 (클릭: 한 곡 반복)",
        RepeatMode.RepeatOne => "한 곡 반복 (클릭: 끔)",
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
        }
    }
    public string ShuffleTooltip => _shuffle
        ? "셔플 켜짐 (클릭: 끔)"
        : "셔플 꺼짐 (클릭: 랜덤 재생)";

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
            Title = "미디어 파일 열기",
            CheckFileExists = true,
            Multiselect = false,
            Filter = BuildFileFilter(),
            InitialDirectory = Settings.LastOpenedFolder ?? ""
        };
        if (dlg.ShowDialog() == true)
            OpenPath(dlg.FileName);
    }

    private static string BuildFileFilter()
    {
        var all = MediaKindExtensions.AllSupportedExtensions()
            .Select(e => "*" + e).Distinct().OrderBy(s => s);
        var allJoined = string.Join(";", all);
        return $"모든 지원 미디어|{allJoined}|모든 파일|*.*";
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
            StatusMessage = $"파일을 찾을 수 없습니다: {path}";
            return;
        }
        if (!MediaKindExtensions.IsSupported(path))
        {
            State = PlayerState.Failed;
            StatusMessage = $"지원하지 않는 형식: {Path.GetExtension(path)}";
            return;
        }
        if (!_ipc.IsConnected)
        {
            State = PlayerState.Failed;
            StatusMessage = "mpv 백엔드 연결이 끊겼습니다. 앱을 다시 시작해주세요.";
            return;
        }

        var folder = Path.GetDirectoryName(path);
        Settings.LastOpenedFolder = folder;

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
        media.HasError = false;        // 재시도 = 에러 마크 클리어
        media.ErrorMessage = null;
        CurrentMedia = media;
        FileNameDisplay = media.FileName;
        State = PlayerState.Loading;
        StatusMessage = "로딩 중...";
        IsAtEnd = false;
        _ = _ipc.LoadFile(media.FullPath);
        _ = _ipc.SetPause(false);

        // 오디오 파일이면 mpv lavfi 비주얼라이저로 파형을 비디오 출력에 그림 → 검은 화면 대신.
        // 비디오/이미지는 lavfi-complex 클리어해서 기본 패스로 복귀.
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
        if (!HasNext) return;
        PlayMedia(Playlist[CurrentIndex + 1]);
    }

    private void Prev()
    {
        if (!HasPrev) return;
        PlayMedia(Playlist[CurrentIndex - 1]);
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

    private void TakeScreenshot()
    {
        Services.AppLog.Info($"TakeScreenshot invoked: media={CurrentMedia?.FileName ?? "(null)"} ipc={_ipc.IsConnected}");
        if (CurrentMedia is null)
        {
            Toast?.Invoke("재생 중인 미디어가 없습니다");
            return;
        }
        if (!_ipc.IsConnected)
        {
            Toast?.Invoke("mpv 연결이 끊겼습니다 — 앱 재시작 필요");
            return;
        }
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                "DenoPlayer");
            Directory.CreateDirectory(dir);
            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var raw  = Path.GetFileNameWithoutExtension(CurrentMedia.FileName);
            // mpv가 일부 유니코드/특수문자 경로에서 실패하는 경우가 있어 file-name 안전화
            var safe = string.Concat(raw.Where(c => !Path.GetInvalidFileNameChars().Contains(c)));
            if (string.IsNullOrWhiteSpace(safe)) safe = "screenshot";
            var path = Path.Combine(dir, $"{safe}_{stamp}.png");
            Services.AppLog.Info($"Screenshot -> {path}");
            _ = _ipc.Screenshot(path);
            Toast?.Invoke($"스크린샷 저장 → {dir}");
        }
        catch (Exception ex)
        {
            Services.AppLog.Error("Screenshot", ex);
            Toast?.Invoke("스크린샷 실패: " + ex.Message);
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
    public void EndSeek(double seconds)
    {
        Seeking = false;
        _ = _ipc.SeekAbsolute(seconds);
        // EOF 상태에서 seek = 그 위치부터 다시 보고 싶다는 의도 → 자동 재생
        if (IsAtEnd)
        {
            _ = _ipc.SetPause(false);
            IsAtEnd = false;
        }
    }

    // mpv 이벤트가 매 frame (60Hz+) 옴 → UI 스레드 dispatch 폭주를 막아 키 입력 응답성 확보
    private DateTime _lastTimePosAt;
    private DateTime _lastMouseActAt;

    private void OnMpvPropertyChanged(string name, JsonElement? value)
    {
        // throttle을 dispatcher 진입 전에 — 큐 자체를 비움
        if (name == "time-pos")
        {
            var now = DateTime.UtcNow;
            if (now - _lastTimePosAt < TimeSpan.FromMilliseconds(120)) return;
            _lastTimePosAt = now;
        }
        else if (name == "mouse-pos")
        {
            var now = DateTime.UtcNow;
            if (now - _lastMouseActAt < TimeSpan.FromMilliseconds(150)) return;
            _lastMouseActAt = now;
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
                case "mouse-pos":
                    if (value is { ValueKind: JsonValueKind.Object } obj)
                    {
                        var x = obj.TryGetProperty("x", out var xe) && xe.TryGetDouble(out var xd) ? xd : 0;
                        var y = obj.TryGetProperty("y", out var ye) && ye.TryGetDouble(out var yd) ? yd : 0;
                        MpvMousePos?.Invoke(x, y);
                    }
                    MouseActivity?.Invoke();
                    break;
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
                    CurrentMedia.ErrorMessage = "재생 실패";
                }
                State = PlayerState.Failed;
                StatusMessage = "이 파일을 재생할 수 없습니다.";
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

    /// <summary>재생목록 처음부터 같은 kind인 첫 항목 (RepeatAll wrap용).</summary>
    private MediaItem? FirstOfSameKind()
    {
        if (CurrentMedia is null) return null;
        var kind = CurrentMedia.Kind;
        return Playlist.FirstOrDefault(m => m.Kind == kind);
    }

    /// <summary>같은 kind 중 현재 곡 외에서 랜덤 하나 (Shuffle 자동 next).</summary>
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
        // 폴더 드롭이면 첫 미디어 파일 사용
        foreach (var f in files)
        {
            if (Directory.Exists(f))
            {
                var first = Directory.EnumerateFiles(f)
                    .Where(MediaKindExtensions.IsSupported)
                    .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault();
                if (first is not null) { OpenPath(first); return true; }
                continue;
            }
            if (MediaKindExtensions.IsSupported(f))
            {
                OpenPath(f);
                return true;
            }
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

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Raise([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    private bool Set<T>(ref T field, T value, [CallerMemberName] string? n = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value; Raise(n);
        return true;
    }

    public void Dispose() => _ipc.Dispose();
}

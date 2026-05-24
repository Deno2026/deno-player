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

    public MainViewModel(MpvProcessService mpvProc)
    {
        _mpvProc = mpvProc;
        _ui = Application.Current.Dispatcher;
        Settings = _settingsSvc.Load();
        _volume = Settings.Volume;
        _muted = Settings.Muted;
        _speed = Settings.PlaybackRate;
        _isPlaylistOpen = Settings.PlaylistPanelEnabled;

        PlayPauseCommand = new RelayCommand(TogglePlayPause);
        NextCommand      = new RelayCommand(Next, () => HasNext);
        PrevCommand      = new RelayCommand(Prev, () => HasPrev);
        OpenCommand      = new RelayCommand(OpenDialog);
        MuteCommand      = new RelayCommand(() => IsMuted = !IsMuted);
        FullscreenCommand= new RelayCommand(() => IsFullscreen = !IsFullscreen);
        ScreenshotCommand= new RelayCommand(TakeScreenshot);
        AlwaysOnTopCommand=new RelayCommand(() => IsAlwaysOnTop = !IsAlwaysOnTop);
        TogglePlaylistCommand = new RelayCommand(() => IsPlaylistOpen = !IsPlaylistOpen);
        Seek5ForwardCommand   = new RelayCommand(() => _ = _ipc.SeekRelative(5));
        Seek5BackwardCommand  = new RelayCommand(() => _ = _ipc.SeekRelative(-5));
        Seek30ForwardCommand  = new RelayCommand(() => _ = _ipc.SeekRelative(30));
        Seek30BackwardCommand = new RelayCommand(() => _ = _ipc.SeekRelative(-30));
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
        var ok = await _ipc.ConnectAsync(_mpvProc.PipeName, TimeSpan.FromSeconds(5));
        if (!ok)
        {
            StatusMessage = "mpv IPC 연결 실패";
            State = PlayerState.Failed;
        }
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
    public PlayerState State { get => _state; set => Set(ref _state, value); }

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
        private set { if (Set(ref _currentMedia, value)) { Raise(nameof(HasMedia)); Raise(nameof(HasNext)); Raise(nameof(HasPrev)); } }
    }
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

    // ============================================================
    // commands
    // ============================================================

    public RelayCommand PlayPauseCommand { get; }
    public RelayCommand NextCommand { get; }
    public RelayCommand PrevCommand { get; }
    public RelayCommand OpenCommand { get; }
    public RelayCommand MuteCommand { get; }
    public RelayCommand FullscreenCommand { get; }
    public RelayCommand ScreenshotCommand { get; }
    public RelayCommand AlwaysOnTopCommand { get; }
    public RelayCommand TogglePlaylistCommand { get; }
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
        if (string.IsNullOrWhiteSpace(path)) return;
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
        if (media is null) return;
        foreach (var m in Playlist) m.IsPlaying = false;
        media.IsPlaying = true;
        CurrentMedia = media;
        FileNameDisplay = media.FileName;
        State = PlayerState.Loading;
        StatusMessage = "로딩 중...";
        _ = _ipc.LoadFile(media.FullPath);
        // 일시정지 상태에서 다음 곡으로 넘어가도 자동 재생되도록 명시
        // (mpv는 loadfile 직후 이전 pause 상태를 유지함)
        _ = _ipc.SetPause(false);
        if (_isPaused) IsPaused = false;
        NextCommand.RaiseCanExecuteChanged();
        PrevCommand.RaiseCanExecuteChanged();
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

    private void TakeScreenshot()
    {
        if (CurrentMedia is null) return;
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                "DenoPlayer");
            Directory.CreateDirectory(dir);
            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var name = Path.GetFileNameWithoutExtension(CurrentMedia.FileName);
            var path = Path.Combine(dir, $"{name}_{stamp}.png");
            _ = _ipc.Screenshot(path);
            StatusMessage = $"스크린샷 저장: {path}";
        }
        catch (Exception ex)
        {
            StatusMessage = "스크린샷 실패: " + ex.Message;
        }
    }

    // ============================================================
    // mpv events
    // ============================================================

    private bool _seeking;
    public bool Seeking { get => _seeking; set => Set(ref _seeking, value); }

    public void BeginSeek() => Seeking = true;
    public void EndSeek(double seconds)
    {
        Seeking = false;
        _ = _ipc.SeekAbsolute(seconds);
    }

    private void OnMpvPropertyChanged(string name, JsonElement? value)
    {
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
        OnUi(() =>
        {
            State = IsPaused ? PlayerState.Paused : PlayerState.Playing;
            StatusMessage = "";
        });
    }

    private void OnEndFile(string? reason)
    {
        OnUi(() =>
        {
            if (reason == "eof" && Settings.AutoPlayNext && HasNext)
            {
                Next();
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

using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using DenoPlayer.Helpers;

namespace DenoPlayer.Models;

public sealed class MediaItem : INotifyPropertyChanged
{
    public MediaItem(string path)
    {
        FullPath = path;
        FileName = Path.GetFileName(path);
        Kind = MediaKindExtensions.FromPath(path);
    }

    public string FullPath { get; }
    public string FileName { get; }
    public MediaKind Kind { get; }
    public string? Folder => Path.GetDirectoryName(FullPath);

    private double? _durationSeconds;
    public double? DurationSeconds
    {
        get => _durationSeconds;
        set { if (_durationSeconds != value) { _durationSeconds = value; OnChanged(); OnChanged(nameof(DurationDisplay)); } }
    }

    public string DurationDisplay =>
        _durationSeconds is { } d && d > 0 ? TimeFormat.Seconds(d) : "";

    private bool _isPlaying;
    public bool IsPlaying
    {
        get => _isPlaying;
        set { if (_isPlaying != value) { _isPlaying = value; OnChanged(); } }
    }

    private bool _hasError;
    public bool HasError
    {
        get => _hasError;
        set { if (_hasError != value) { _hasError = value; OnChanged(); } }
    }

    private string? _errorMessage;
    public string? ErrorMessage
    {
        get => _errorMessage;
        set { if (_errorMessage != value) { _errorMessage = value; OnChanged(); } }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

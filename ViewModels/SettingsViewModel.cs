using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using DenoPlayer.Models;

namespace DenoPlayer.ViewModels;

public sealed class ExtItem : INotifyPropertyChanged
{
    public string Extension { get; }
    public ExtItem(string ext, bool selected) { Extension = ext; _selected = selected; }
    private bool _selected;
    public bool Selected
    {
        get => _selected;
        set { if (_selected != value) { _selected = value; OnChanged(); } }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public sealed class ExtGroup
{
    public string Title { get; }
    public ObservableCollection<ExtItem> Items { get; }
    public ExtGroup(string title, IEnumerable<ExtItem> items)
    {
        Title = title;
        Items = new ObservableCollection<ExtItem>(items);
    }
    public IEnumerable<string> SelectedExtensions =>
        Items.Where(i => i.Selected).Select(i => i.Extension);

    public bool AllSelected
    {
        get => Items.All(i => i.Selected);
        set
        {
            foreach (var i in Items) i.Selected = value;
        }
    }
}

public sealed class SettingsViewModel
{
    public ObservableCollection<ExtGroup> Groups { get; }

    private static readonly string[] VideoExts =
        { ".mp4", ".mkv", ".mov", ".webm", ".avi", ".m4v", ".ts", ".mts", ".m2ts", ".wmv", ".flv", ".3gp" };
    private static readonly string[] AudioExts =
        { ".mp3", ".wav", ".flac", ".aac", ".m4a", ".ogg", ".opus", ".wma", ".alac" };
    private static readonly string[] ImageExts =
        { ".jpg", ".jpeg", ".png", ".webp", ".bmp", ".gif" };

    public IEnumerable<string> AllKnownExtensions =>
        VideoExts.Concat(AudioExts).Concat(ImageExts);

    public SettingsViewModel(AppSettings settings)
    {
        // 저장된 목록이 있으면 그것 기반, 없으면 default = 전체 선택
        var saved = settings.RegisteredExtensions;
        var preselected = saved is { Count: > 0 }
            ? new HashSet<string>(saved, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(AllKnownExtensions, StringComparer.OrdinalIgnoreCase);

        ExtItem Make(string e) => new(e, preselected.Contains(e));

        Groups = new ObservableCollection<ExtGroup>
        {
            new("동영상", VideoExts.Select(Make)),
            new("오디오", AudioExts.Select(Make)),
            new("이미지", ImageExts.Select(Make)),
        };
    }

    public IEnumerable<string> SelectedExtensions =>
        Groups.SelectMany(g => g.SelectedExtensions);
}

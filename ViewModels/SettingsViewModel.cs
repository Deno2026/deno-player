using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using DenoVideoPlayer.Models;
using DenoVideoPlayer.Services;

namespace DenoVideoPlayer.ViewModels;

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

public sealed class SettingsViewModel : System.ComponentModel.INotifyPropertyChanged
{
    public ObservableCollection<ExtGroup> Groups { get; }

    private string _selectedLanguage = LocalizationService.Korean;
    public string SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            var normalized = LocalizationService.Normalize(value);
            if (_selectedLanguage == normalized) return;
            _selectedLanguage = normalized;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(SelectedLanguage)));
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsKoreanSelected)));
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsEnglishSelected)));
        }
    }

    public bool IsKoreanSelected
    {
        get => SelectedLanguage == LocalizationService.Korean;
        set { if (value) SelectedLanguage = LocalizationService.Korean; }
    }

    public bool IsEnglishSelected
    {
        get => SelectedLanguage == LocalizationService.English;
        set { if (value) SelectedLanguage = LocalizationService.English; }
    }

    private string _screenshotFolder = "";
    /// <summary>스크린샷 PNG 저장 폴더. 빈 문자열이면 기본값(Pictures\Deno Video Player\).</summary>
    public string ScreenshotFolder
    {
        get => _screenshotFolder;
        set
        {
            if (_screenshotFolder == value) return;
            _screenshotFolder = value ?? "";
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(ScreenshotFolder)));
        }
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    private static readonly string[] VideoExts =
        { ".mp4", ".mkv", ".mov", ".webm", ".avi", ".m4v", ".ts", ".mts", ".m2ts", ".wmv", ".flv", ".3gp" };
    private static readonly string[] AudioExts =
        { ".mp3", ".wav", ".flac", ".aac", ".m4a", ".ogg", ".opus", ".wma", ".alac" };
    private static readonly string[] ImageExts =
        { ".jpg", ".jpeg", ".png", ".webp", ".bmp", ".gif" };

    public IEnumerable<string> AllKnownExtensions =>
        VideoExts.Concat(AudioExts).Concat(ImageExts);

    public IEnumerable<string> DefaultRegisteredExtensions =>
        VideoExts.Concat(AudioExts);

    public SettingsViewModel(AppSettings settings)
    {
        // First run defaults to video + audio only. Images remain opt-in.
        var saved = settings.RegisteredExtensions;
        var preselected = saved is not null
            ? new HashSet<string>(saved, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(DefaultRegisteredExtensions, StringComparer.OrdinalIgnoreCase);

        ExtItem Make(string e) => new(e, preselected.Contains(e));

        Groups = new ObservableCollection<ExtGroup>
        {
            new(LocalizationService.T("Video"), VideoExts.Select(Make)),
            new(LocalizationService.T("Audio"), AudioExts.Select(Make)),
            new(LocalizationService.T("Image"), ImageExts.Select(Make)),
        };

        _screenshotFolder = settings.ScreenshotFolder ?? "";
        _selectedLanguage = LocalizationService.Normalize(settings.Language);
    }

    public IEnumerable<string> SelectedExtensions =>
        Groups.SelectMany(g => g.SelectedExtensions);
}

using System.Windows;
using DenoVideoPlayer.Models;
using DenoVideoPlayer.Services;
using DenoVideoPlayer.ViewModels;

namespace DenoVideoPlayer.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _vm;
    private readonly AppSettings _settings;
    private readonly SettingsService _svc;

    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        _svc = new SettingsService();
        _vm = new SettingsViewModel(settings);
        DataContext = _vm;
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();

    private void OnPickScreenshotFolder(object? sender, RoutedEventArgs e)
    {
        try
        {
            // .NET 8 WPF의 OpenFolderDialog 사용
            var dlg = new Microsoft.Win32.OpenFolderDialog
            {
                Title = LocalizationService.T("PickScreenshotFolderTitle"),
                InitialDirectory = !string.IsNullOrWhiteSpace(_vm.ScreenshotFolder)
                    ? _vm.ScreenshotFolder
                    : Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
            };
            if (dlg.ShowDialog(this) == true)
                _vm.ScreenshotFolder = dlg.FolderName;
        }
        catch (Exception ex)
        {
            AppLog.Error("PickScreenshotFolder", ex);
            MessageBox.Show(this, LocalizationService.F("PickFolderFailed", ex.Message), "Deno Video Player",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnResetScreenshotFolder(object? sender, RoutedEventArgs e)
    {
        _vm.ScreenshotFolder = ""; // 빈 = 기본값 (Pictures\Deno Video Player)
    }

    private void OnConfirm(object? sender, RoutedEventArgs e)
    {
        try
        {
            var selected = SaveSettings();
            SyncFileAssociations(selected);
            if (selected.Count > 0)
                FileAssociationService.OpenDefaultAppsSettings();
            Close();
        }
        catch (Exception ex)
        {
            AppLog.Error("Settings confirm", ex);
            MessageBox.Show(this, LocalizationService.F("SettingsApplyError", ex.Message), "Deno Video Player",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnOpenDefaultAppsSettings(object? sender, RoutedEventArgs e)
    {
        try
        {
            var selected = SaveSettings();
            SyncFileAssociations(selected);
            FileAssociationService.OpenDefaultAppsSettings();
        }
        catch (Exception ex)
        {
            AppLog.Error("Settings register file types", ex);
            MessageBox.Show(this, LocalizationService.F("SettingsRegisterError", ex.Message), "Deno Video Player",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private List<string> SaveSettings()
    {
        var selected = _vm.SelectedExtensions.ToList();
        _settings.RegisteredExtensions = selected;
        _settings.ScreenshotFolder = string.IsNullOrWhiteSpace(_vm.ScreenshotFolder)
            ? null
            : _vm.ScreenshotFolder;
        _settings.Language = LocalizationService.Normalize(_vm.SelectedLanguage);
        _svc.Save(_settings);
        LocalizationService.Apply(_settings.Language);
        return selected;
    }

    private void SyncFileAssociations(IReadOnlyCollection<string> selected)
    {
        var exePath = System.IO.Path.Combine(
            AppContext.BaseDirectory, "DenoVideoPlayer.exe");
        FileAssociationService.RegisterApplication(exePath);
        FileAssociationService.SyncExtensions(selected, _vm.AllKnownExtensions);
    }
}

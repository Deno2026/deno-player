using System.Text.Json;
using System.Reflection;
using DenoVideoPlayer.Models;
using DenoVideoPlayer.Services;
using DenoVideoPlayer.ViewModels;

namespace DenoVideoPlayer.Tests;

public class SettingsRoundTripTests
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact] public void DefaultsHaveReasonableValues()
    {
        var s = AppSettings.Defaults();
        Assert.True(s.WindowWidth >= 800);
        Assert.True(s.WindowHeight >= 480);
        Assert.InRange(s.Volume, 0, 100);
        Assert.True(s.AutoPlayNext);
        Assert.True(s.ControlAutoHideMs > 0);
        Assert.Equal(1.0, s.PlaybackRate);
        Assert.Equal("ko", s.Language);
        Assert.Equal(PlaylistSortMode.NameAscending, s.PlaylistSort);
    }

    [Fact] public void JsonRoundTripPreservesAllValues()
    {
        var s = new AppSettings
        {
            WindowWidth = 1024,
            WindowHeight = 600,
            WindowLeft = 100,
            WindowTop = 50,
            WindowMaximized = true,
            Volume = 42,
            Muted = true,
            PlaybackRate = 1.25,
            LastOpenedFolder = @"C:\Users\test\Videos",
            AutoPlayNext = false,
            Language = "en",
            ControlAutoHideMs = 4000,
            PlaylistPanelEnabled = false,
            AlwaysOnTop = true,
            PlaylistSort = PlaylistSortMode.CreatedAscending
        };

        var json = JsonSerializer.Serialize(s, Opts);
        var back = JsonSerializer.Deserialize<AppSettings>(json, Opts)!;

        Assert.Equal(s.WindowWidth, back.WindowWidth);
        Assert.Equal(s.WindowHeight, back.WindowHeight);
        Assert.Equal(s.WindowLeft, back.WindowLeft);
        Assert.Equal(s.WindowTop, back.WindowTop);
        Assert.Equal(s.WindowMaximized, back.WindowMaximized);
        Assert.Equal(s.Volume, back.Volume);
        Assert.Equal(s.Muted, back.Muted);
        Assert.Equal(s.PlaybackRate, back.PlaybackRate);
        Assert.Equal(s.LastOpenedFolder, back.LastOpenedFolder);
        Assert.Equal(s.AutoPlayNext, back.AutoPlayNext);
        Assert.Equal(s.Language, back.Language);
        Assert.Equal(s.ControlAutoHideMs, back.ControlAutoHideMs);
        Assert.Equal(s.PlaylistPanelEnabled, back.PlaylistPanelEnabled);
        Assert.Equal(s.AlwaysOnTop, back.AlwaysOnTop);
        Assert.Equal(s.PlaylistSort, back.PlaylistSort);
    }

    [Fact] public void NewPropertyDoesNotBreakOldFile()
    {
        // 일부 키만 있는 옛 settings.json
        var partial = "{\"volume\":55, \"muted\":false}";
        var back = JsonSerializer.Deserialize<AppSettings>(partial, Opts)!;
        Assert.Equal(55, back.Volume);
        // 누락 키는 default
        Assert.Equal(1.0, back.PlaybackRate);
        Assert.True(back.AutoPlayNext);
        Assert.Equal("ko", back.Language);
        Assert.Equal(PlaylistSortMode.NameAscending, back.PlaylistSort);
    }

    [Fact] public void NormalizeRepairsUnsafeOrCorruptValues()
    {
        var s = new AppSettings
        {
            WindowWidth = double.PositiveInfinity,
            WindowHeight = -50,
            WindowLeft = double.NaN,
            Volume = 999,
            PlaybackRate = -2,
            Language = "unsupported",
            ControlAutoHideMs = -1,
            RepeatMode = 99,
            PlaylistSort = (PlaylistSortMode)99,
            RegisteredExtensions = new List<string> { "MP4", ".mp4", @"..\bad", "" },
            RecentFiles = Enumerable.Repeat(@"C:\same.mp4", 40).Concat(
                Enumerable.Range(0, 40).Select(i => $@"C:\file-{i}.mp4")).ToList(),
            ScreenshotFolder = "   "
        }.Normalize();

        Assert.Equal(1280, s.WindowWidth);
        Assert.Equal(320, s.WindowHeight);
        Assert.Null(s.WindowLeft);
        Assert.Equal(100, s.Volume);
        Assert.Equal(0.25, s.PlaybackRate);
        Assert.Equal("ko", s.Language);
        Assert.Equal(250, s.ControlAutoHideMs);
        Assert.Equal(2, s.RepeatMode);
        Assert.Equal(PlaylistSortMode.NameAscending, s.PlaylistSort);
        Assert.Equal(new[] { ".mp4" }, s.RegisteredExtensions);
        Assert.Equal(30, s.RecentFiles!.Count);
        Assert.Equal(s.RecentFiles.Count, s.RecentFiles.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Null(s.ScreenshotFolder);
    }

    [Fact] public void EnglishLocalizationCanBeSelected()
    {
        LocalizationService.Apply("en");

        Assert.Equal("Open file", LocalizationService.T("OpenFile"));
        Assert.Equal("New version 0.4.1 — click to update",
            LocalizationService.F("UpdateAvailable", "0.4.1"));
        Assert.Equal("Register extensions", LocalizationService.T("RegisterFileTypes"));
        Assert.Equal("Open Windows Default Apps", LocalizationService.T("OpenDefaultAppsSettings"));
        Assert.Contains("Could not save settings",
            LocalizationService.F("SettingsSaveError", "sample"));
        Assert.Contains("Could not apply settings",
            LocalizationService.F("SettingsApplyError", "sample"));

        LocalizationService.Apply("ko");
    }

    [Fact]
    public void KoreanAndEnglishLocalizationKeysStayInSync()
    {
        var flags = BindingFlags.NonPublic | BindingFlags.Static;
        var korean = Assert.IsType<Dictionary<string, string>>(
            typeof(LocalizationService).GetField("KoreanStrings", flags)!.GetValue(null));
        var english = Assert.IsType<Dictionary<string, string>>(
            typeof(LocalizationService).GetField("EnglishStrings", flags)!.GetValue(null));

        Assert.Equal(
            korean.Keys.Order(StringComparer.Ordinal),
            english.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void BuiltInHelpIsAvailableInBothLanguages()
    {
        try
        {
            LocalizationService.Apply("ko");
            Assert.Equal("빠른 사용법", LocalizationService.T("HelpHeader"));
            Assert.Contains("F1", LocalizationService.T("TooltipHelp"));
            Assert.Contains("Ctrl+H", LocalizationService.T("TooltipRecent"));
            Assert.Contains("Ctrl+H", LocalizationService.T("HelpQuickPanelsBody"));
            Assert.Contains("문제 해결", LocalizationService.T("HelpTroubleshootingHeader"));
            Assert.Contains("FFmpeg", LocalizationService.T("HelpTroubleshootingExport"));
            Assert.Equal("재생 엔진을 사용할 수 없습니다",
                LocalizationService.T("PlaybackEngineFailed"));
            Assert.Contains("재생 엔진을 다시 시도",
                LocalizationService.T("MpvDisconnectedRestart"));

            LocalizationService.Apply("en");
            Assert.Equal("Quick guide", LocalizationService.T("HelpHeader"));
            Assert.Contains("F1", LocalizationService.T("TooltipHelp"));
            Assert.Contains("Ctrl+H", LocalizationService.T("TooltipRecent"));
            Assert.Contains("Ctrl+H", LocalizationService.T("HelpQuickPanelsBody"));
            Assert.Equal("Troubleshooting",
                LocalizationService.T("HelpTroubleshootingHeader"));
            Assert.Contains("FFmpeg", LocalizationService.T("HelpTroubleshootingExport"));
            Assert.Equal("The playback engine is unavailable",
                LocalizationService.T("PlaybackEngineFailed"));
            Assert.Contains("Retry the playback engine",
                LocalizationService.T("MpvDisconnectedRestart"));
        }
        finally
        {
            LocalizationService.Apply("ko");
        }
    }

    [Fact]
    public void RecentPanelCommandRequestsAnExplicitToggle()
    {
        using var viewModel = new MainViewModel(
            new MpvProcessService(),
            AppSettings.Defaults());
        var requested = false;
        viewModel.RecentToggleRequested += () => requested = true;

        viewModel.ToggleRecentCommand.Execute(null);

        Assert.True(requested);
        viewModel.SetRecentOpen(true);
        Assert.True(viewModel.IsRecentOpen);
        viewModel.SetRecentOpen(false);
        Assert.False(viewModel.IsRecentOpen);
    }

    [Fact]
    public void LocalizedStatusMessagesRefreshWhenLanguageChanges()
    {
        var originalLanguage = LocalizationService.CurrentLanguage;
        MainViewModel? viewModel = null;
        try
        {
            LocalizationService.Apply("ko");
            viewModel = new MainViewModel(
                new MpvProcessService(),
                AppSettings.Defaults());

            viewModel.SetLocalizedFailure(
                PlayerFailureKind.Backend,
                "MpvDisconnectedRestart");
            Assert.Equal(
                LocalizationService.T("MpvDisconnectedRestart"),
                viewModel.StatusMessage);

            LocalizationService.Apply("en");
            Assert.Equal(
                LocalizationService.T("MpvDisconnectedRestart"),
                viewModel.StatusMessage);

            viewModel.SetLocalizedFailure(
                PlayerFailureKind.Media,
                "FileNotFound",
                @"C:\missing\sample.mp4");
            Assert.Equal(
                LocalizationService.F(
                    "FileNotFound",
                    @"C:\missing\sample.mp4"),
                viewModel.StatusMessage);

            LocalizationService.Apply("ko");
            Assert.Equal(
                LocalizationService.F(
                    "FileNotFound",
                    @"C:\missing\sample.mp4"),
                viewModel.StatusMessage);

            viewModel.SetLocalizedFailure(
                PlayerFailureKind.Backend,
                "MpvStartFailed",
                new LocalizedText("MpvIpcFailed"));
            Assert.Equal(
                LocalizationService.F(
                    "MpvStartFailed",
                    new LocalizedText("MpvIpcFailed")),
                viewModel.StatusMessage);

            LocalizationService.Apply("en");
            Assert.Equal(
                LocalizationService.F(
                    "MpvStartFailed",
                    new LocalizedText("MpvIpcFailed")),
                viewModel.StatusMessage);

            LocalizationService.Apply("ko");
            var localizedException = new LocalizedDetailException(
                new LocalizedText(
                    "MpvExecutableMissing",
                    @"C:\runtime\mpv.exe"));
            viewModel.SetLocalizedFailure(
                PlayerFailureKind.Backend,
                "MpvStartFailed",
                localizedException.Detail);
            Assert.Equal(
                LocalizationService.F(
                    "MpvStartFailed",
                    new LocalizedText(
                        "MpvExecutableMissing",
                        @"C:\runtime\mpv.exe")),
                viewModel.StatusMessage);

            LocalizationService.Apply("en");
            Assert.Equal(
                LocalizationService.F(
                    "MpvStartFailed",
                    new LocalizedText(
                        "MpvExecutableMissing",
                        @"C:\runtime\mpv.exe")),
                viewModel.StatusMessage);

            viewModel.SetLocalizedStatus("LoadingStatus");
            Assert.Equal(
                LocalizationService.T("LoadingStatus"),
                viewModel.StatusMessage);
        }
        finally
        {
            viewModel?.Dispose();
            LocalizationService.Apply(originalLanguage);
        }
    }
}

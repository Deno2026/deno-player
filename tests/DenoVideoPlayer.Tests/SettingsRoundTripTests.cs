using System.Text.Json;
using DenoVideoPlayer.Models;
using DenoVideoPlayer.Services;

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
}

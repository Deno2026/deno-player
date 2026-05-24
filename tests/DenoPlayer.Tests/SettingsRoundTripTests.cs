using System.Text.Json;
using DenoPlayer.Models;

namespace DenoPlayer.Tests;

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
            ControlAutoHideMs = 4000,
            PlaylistPanelEnabled = false,
            AlwaysOnTop = true
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
        Assert.Equal(s.ControlAutoHideMs, back.ControlAutoHideMs);
        Assert.Equal(s.PlaylistPanelEnabled, back.PlaylistPanelEnabled);
        Assert.Equal(s.AlwaysOnTop, back.AlwaysOnTop);
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
    }
}

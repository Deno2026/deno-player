using DenoVideoPlayer.Models;
using DenoVideoPlayer.ViewModels;

namespace DenoVideoPlayer.Tests;

public class SettingsViewModelTests
{
    [Fact]
    public void FirstRunFileAssociationsDefaultToVideoAndAudioOnly()
    {
        var vm = new SettingsViewModel(new AppSettings { RegisteredExtensions = null });
        var selected = vm.SelectedExtensions.ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains(".mp4", selected);
        Assert.Contains(".mkv", selected);
        Assert.Contains(".mp3", selected);
        Assert.Contains(".wav", selected);
        Assert.DoesNotContain(".jpg", selected);
        Assert.DoesNotContain(".png", selected);
        Assert.DoesNotContain(".gif", selected);
    }

    [Fact]
    public void ExplicitEmptyFileAssociationsStayEmpty()
    {
        var vm = new SettingsViewModel(new AppSettings { RegisteredExtensions = new List<string>() });

        Assert.Empty(vm.SelectedExtensions);
    }

    [Fact]
    public void ExplicitImageSelectionIsPreserved()
    {
        var vm = new SettingsViewModel(new AppSettings
        {
            RegisteredExtensions = new List<string> { ".mp4", ".png" }
        });
        var selected = vm.SelectedExtensions.ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains(".mp4", selected);
        Assert.Contains(".png", selected);
        Assert.DoesNotContain(".mp3", selected);
    }
}

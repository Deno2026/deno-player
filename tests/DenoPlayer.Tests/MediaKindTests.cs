using DenoPlayer.Models;

namespace DenoPlayer.Tests;

public class MediaKindTests
{
    [Theory]
    [InlineData("clip.mp4", MediaKind.Video)]
    [InlineData("clip.MP4", MediaKind.Video)]
    [InlineData("clip.mkv", MediaKind.Video)]
    [InlineData("c:\\path\\with space.mov", MediaKind.Video)]
    [InlineData("a.webm", MediaKind.Video)]
    [InlineData("a.mts", MediaKind.Video)]
    public void DetectsVideo(string path, MediaKind expected)
        => Assert.Equal(expected, MediaKindExtensions.FromPath(path));

    [Theory]
    [InlineData("song.mp3", MediaKind.Audio)]
    [InlineData("track.FLAC", MediaKind.Audio)]
    [InlineData("voice.opus", MediaKind.Audio)]
    public void DetectsAudio(string path, MediaKind expected)
        => Assert.Equal(expected, MediaKindExtensions.FromPath(path));

    [Theory]
    [InlineData("pic.jpg", MediaKind.Image)]
    [InlineData("pic.JPEG", MediaKind.Image)]
    [InlineData("pic.png", MediaKind.Image)]
    [InlineData("pic.webp", MediaKind.Image)]
    [InlineData("pic.gif", MediaKind.Image)]
    public void DetectsImage(string path, MediaKind expected)
        => Assert.Equal(expected, MediaKindExtensions.FromPath(path));

    [Theory]
    [InlineData("readme.txt")]
    [InlineData("archive.zip")]
    [InlineData("doc.pdf")]
    [InlineData("no_extension")]
    [InlineData("")]
    public void UnknownExtensions(string path)
        => Assert.Equal(MediaKind.Unknown, MediaKindExtensions.FromPath(path));

    [Fact] public void NullPathReturnsUnknown()
        => Assert.Equal(MediaKind.Unknown, MediaKindExtensions.FromPath(null!));

    [Fact] public void AllSupportedListContainsExpected()
    {
        var all = MediaKindExtensions.AllSupportedExtensions().ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains(".mp4", all);
        Assert.Contains(".mp3", all);
        Assert.Contains(".png", all);
        Assert.DoesNotContain(".txt", all);
    }

    [Fact] public void IsSupportedMatchesFromPath()
    {
        Assert.True(MediaKindExtensions.IsSupported("a.mp4"));
        Assert.False(MediaKindExtensions.IsSupported("a.txt"));
    }
}

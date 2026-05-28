using DenoVideoPlayer.Models;

namespace DenoVideoPlayer.Tests;

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

    [Theory]
    [InlineData("ko.srt", true)]
    [InlineData("en.SRT", true)]
    [InlineData("style.ass", true)]
    [InlineData("track.vtt", true)]
    [InlineData("clip.mp4", false)]     // 미디어는 IsSubtitle=false
    [InlineData("readme.txt", false)]
    [InlineData("", false)]
    public void DetectsSubtitle(string path, bool expected)
        => Assert.Equal(expected, MediaKindExtensions.IsSubtitle(path));

    [Fact] public void IsSupportedAndIsSubtitleAreDistinct()
    {
        // 자막은 미디어가 아니어야 함 — drop 처리에서 갈래가 다르므로
        Assert.False(MediaKindExtensions.IsSupported("ko.srt"));
        Assert.True (MediaKindExtensions.IsSubtitle("ko.srt"));
    }
}

using System.IO;
using DenoVideoPlayer.Services;

namespace DenoVideoPlayer.Tests;

public class PlaylistServiceTests : IDisposable
{
    private readonly string _dir;
    private readonly PlaylistService _svc = new();

    public PlaylistServiceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "DenoVideoPlayerTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private string Touch(string name, DateTime? createdUtc = null)
    {
        var p = Path.Combine(_dir, name);
        File.WriteAllText(p, "");
        if (createdUtc is { } t)
        {
            File.SetCreationTimeUtc(p, t);
            File.SetLastWriteTimeUtc(p, t);
        }
        return p;
    }

    [Fact] public void EmptyFolderReturnsAtLeastTheGivenFile()
    {
        // 폴더 enumeration이 비어도 시드 파일은 포함되어야 — 호출자가 안전하게 한 곡 재생 가능
        var seed = Touch("clip.mp4");
        var list = _svc.BuildFromFile(seed);
        Assert.Single(list);
        Assert.Equal(seed, list[0].FullPath);
    }

    [Fact] public void FiltersUnsupportedExtensionsAndOtherKinds()
    {
        // 시드가 .mp4(video)면 다른 kind(.mp3=audio)도 제외, 미지원(.txt/.zip)도 당연히 제외.
        Touch("a.mp4");
        Touch("readme.txt");
        Touch("archive.zip");
        Touch("b.mp3");
        var list = _svc.BuildFromFile(Path.Combine(_dir, "a.mp4"));
        Assert.Single(list);
        Assert.Equal("a.mp4", list[0].FileName);
    }

    [Fact] public void SortsNewestCreationTimeFirst()
    {
        var baseTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        Touch("clip_1.mp4", baseTime.AddMinutes(1));
        Touch("clip_2.mp4", baseTime.AddMinutes(3));
        Touch("clip_10.mp4", baseTime.AddMinutes(2));
        var list = _svc.BuildFromFile(Path.Combine(_dir, "clip_1.mp4"));
        Assert.Equal(new[] { "clip_2.mp4", "clip_10.mp4", "clip_1.mp4" },
                     list.Select(m => m.FileName).ToArray());
    }

    [Fact] public void NaturallySortsItemsWithSameCreationTime()
    {
        var sameTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        Touch("clip_10.mp4", sameTime);
        Touch("clip_2.mp4", sameTime);
        Touch("clip_1.mp4", sameTime);
        var list = _svc.BuildFromFile(Path.Combine(_dir, "clip_1.mp4"));
        Assert.Equal(new[] { "clip_1.mp4", "clip_2.mp4", "clip_10.mp4" },
                     list.Select(m => m.FileName).ToArray());
    }

    [Fact] public void IndexOfFindsItem()
    {
        var baseTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        Touch("a.mp4", baseTime.AddMinutes(1));
        var b = Touch("b.mp4", baseTime.AddMinutes(2));
        Touch("c.mp4", baseTime.AddMinutes(3));
        var list = _svc.BuildFromFile(Path.Combine(_dir, "a.mp4"));
        Assert.Equal(1, _svc.IndexOf(list, b));
    }

    [Fact] public void IndexOfReturnsMinusOneForMiss()
    {
        Touch("a.mp4");
        var list = _svc.BuildFromFile(Path.Combine(_dir, "a.mp4"));
        Assert.Equal(-1, _svc.IndexOf(list, "z:\\nope.mp4"));
    }

    [Fact] public void MissingFileReturnsEmpty()
    {
        var list = _svc.BuildFromFile(Path.Combine(_dir, "does-not-exist.mp4"));
        Assert.Empty(list);
    }

    [Fact] public void NullOrEmptyPathReturnsEmpty()
    {
        Assert.Empty(_svc.BuildFromFile(""));
        Assert.Empty(_svc.BuildFromFile(null!));
    }

    [Fact] public void OnlySameKindAsSeedIncluded()
    {
        // 사용자가 동영상을 더블클릭하면 재생목록엔 동영상만 (음악·이미지 제외).
        Touch("vid.mp4");
        Touch("aud.mp3");
        Touch("img.png");
        var list = _svc.BuildFromFile(Path.Combine(_dir, "vid.mp4"));
        Assert.Single(list);
        Assert.Equal("vid.mp4", list[0].FileName);
    }

    [Fact] public void AudioSeedGivesAudioOnlyList()
    {
        Touch("vid.mp4");
        Touch("aud_a.mp3");
        Touch("aud_b.flac");
        Touch("img.png");
        var list = _svc.BuildFromFile(Path.Combine(_dir, "aud_a.mp3"));
        Assert.Equal(2, list.Count);
        Assert.All(list, m => Assert.Contains(System.IO.Path.GetExtension(m.FileName).ToLowerInvariant(),
                                              new[] { ".mp3", ".flac" }));
    }

    [Fact] public void IndexOfIsCaseInsensitive()
    {
        Touch("Movie.mp4");
        var list = _svc.BuildFromFile(Path.Combine(_dir, "Movie.mp4"));
        Assert.Equal(0, _svc.IndexOf(list, Path.Combine(_dir, "MOVIE.MP4")));
    }
}

using DenoVideoPlayer.Helpers;

namespace DenoVideoPlayer.Tests;

public class NaturalStringComparerTests
{
    private static int Cmp(string a, string b) => NaturalStringComparer.Instance.Compare(a, b);

    [Fact] public void NullsSortFirst()
    {
        Assert.True(Cmp(null!, "a") < 0);
        Assert.True(Cmp("a", null!) > 0);
        Assert.Equal(0, Cmp(null!, null!));
    }

    [Fact] public void EqualStringsReturnZero()
    {
        Assert.Equal(0, Cmp("file.mp4", "file.mp4"));
    }

    [Fact] public void DigitsCompareNumerically_NotLexically()
    {
        // 일반 string.Compare면 "file10" < "file2"
        Assert.True(Cmp("file2.mp4", "file10.mp4") < 0);
        Assert.True(Cmp("file10.mp4", "file2.mp4") > 0);
    }

    [Fact] public void LeadingZerosShorterRunWinsAtEqualValue()
    {
        // Windows 탐색기와 동일: 숫자 값이 같으면 짧은(leading-zero 없는) 쪽이 앞.
        // "file2" < "file002"
        Assert.True(Cmp("file2.mp4", "file002.mp4") < 0);
        // 자릿수가 더 많으면 값이 더 크므로 뒤. 100 > 002
        Assert.True(Cmp("file100.mp4", "file002.mp4") > 0);
    }

    [Fact] public void MixedAlphaNumeric()
    {
        var list = new[] { "b10", "a2", "b2", "a10", "a1" };
        Array.Sort(list, NaturalStringComparer.Instance);
        Assert.Equal(new[] { "a1", "a2", "a10", "b2", "b10" }, list);
    }

    [Fact] public void CaseInsensitive()
    {
        Assert.Equal(0, Cmp("File.mp4", "file.mp4"));
    }

    [Fact] public void ExplorerStyleVideoNames()
    {
        var list = new[]
        {
            "clip_10.mp4", "clip_2.mp4", "clip_1.mp4",
            "clip_20.mp4", "clip_3.mp4", "clip_100.mp4"
        };
        Array.Sort(list, NaturalStringComparer.Instance);
        Assert.Equal(
            new[] { "clip_1.mp4", "clip_2.mp4", "clip_3.mp4", "clip_10.mp4", "clip_20.mp4", "clip_100.mp4" },
            list);
    }

    [Fact] public void NumericPrefix()
    {
        var list = new[] { "10_intro.mp4", "2_intro.mp4", "1_intro.mp4" };
        Array.Sort(list, NaturalStringComparer.Instance);
        Assert.Equal(new[] { "1_intro.mp4", "2_intro.mp4", "10_intro.mp4" }, list);
    }
}

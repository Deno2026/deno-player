using DenoVideoPlayer.Helpers;

namespace DenoVideoPlayer.Tests;

public class TimeFormatTests
{
    [Theory]
    [InlineData(0, "00:00")]
    [InlineData(5, "00:05")]
    [InlineData(59, "00:59")]
    [InlineData(60, "01:00")]
    [InlineData(125, "02:05")]
    [InlineData(3599, "59:59")]
    public void SubHourFormatsAsMinSec(double seconds, string expected)
        => Assert.Equal(expected, TimeFormat.Seconds(seconds));

    [Theory]
    [InlineData(3600, "1:00:00")]
    [InlineData(3661, "1:01:01")]
    [InlineData(36000, "10:00:00")]
    public void OverHourFormatsWithHours(double seconds, string expected)
        => Assert.Equal(expected, TimeFormat.Seconds(seconds));

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(-1)]
    public void InvalidValuesReturnPlaceholder(double seconds)
        => Assert.Equal("--:--", TimeFormat.Seconds(seconds));

    [Fact] public void FractionalSecondsAreTruncated()
    {
        // TimeSpan.FromSeconds(65.9).Seconds == 5 (truncated to 1:05)
        Assert.Equal("01:05", TimeFormat.Seconds(65.9));
    }
}

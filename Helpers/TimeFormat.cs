namespace DenoPlayer.Helpers;

public static class TimeFormat
{
    /// <summary>초 단위 → mm:ss 또는 h:mm:ss. 음수/NaN/Infinity는 "--:--".</summary>
    public static string Seconds(double seconds)
    {
        if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0) return "--:--";
        var total = TimeSpan.FromSeconds(seconds);
        return total.TotalHours >= 1
            ? $"{(int)total.TotalHours}:{total.Minutes:D2}:{total.Seconds:D2}"
            : $"{total.Minutes:D2}:{total.Seconds:D2}";
    }
}

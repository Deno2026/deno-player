using System.Globalization;
using System.Windows;
using System.Windows.Data;
using DenoVideoPlayer.ViewModels;

namespace DenoVideoPlayer.Helpers;

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }
    public Visibility WhenFalse { get; set; } = Visibility.Collapsed;
    public object Convert(object? value, Type targetType, object? p, CultureInfo c)
    {
        var b = value is bool bb && bb;
        if (Invert) b = !b;
        return b ? Visibility.Visible : WhenFalse;
    }
    public object ConvertBack(object? value, Type targetType, object? p, CultureInfo c)
        => Binding.DoNothing;
}

public sealed class PlayPauseIconConverter : IValueConverter
{
    // Segoe Fluent / MDL2 glyphs
    public object Convert(object? value, Type targetType, object? p, CultureInfo c)
        => value is bool paused && paused ? "" /* Play */ : "" /* Pause */;
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => Binding.DoNothing;
}

public sealed class MuteIconConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? p, CultureInfo c)
        => value is bool m && m ? "" /* Mute */ : "" /* Volume */;
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => Binding.DoNothing;
}

public sealed class FullscreenIconConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? p, CultureInfo c)
        => value is bool fs && fs ? "" /* BackToWindow */ : "" /* FullScreen */;
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => Binding.DoNothing;
}

public sealed class StateToVisibilityConverter : IValueConverter
{
    public string Show { get; set; } = "";              // 표시할 상태 목록(콤마)
    public Visibility Otherwise { get; set; } = Visibility.Collapsed;
    public object Convert(object? value, Type targetType, object? p, CultureInfo c)
    {
        if (value is not PlayerState s) return Otherwise;
        var allowed = Show.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var a in allowed)
            if (string.Equals(a, s.ToString(), StringComparison.OrdinalIgnoreCase))
                return Visibility.Visible;
        return Otherwise;
    }
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => Binding.DoNothing;
}

public sealed class NotNullToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? p, CultureInfo c)
        => value is not null;
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => Binding.DoNothing;
}

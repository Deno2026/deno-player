using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using DenoVideoPlayer.Models;
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

public sealed class MediaKindIconGeometryConverter : IValueConverter
{
    private static readonly Geometry VideoGeometry = CreateVideoGeometry();
    private static readonly Geometry AudioGeometry = Freeze(Geometry.Parse(
        "M8.6,2.2 L13,3.4 L13,5.1 L10,4.3 L10,10.1 C10,11.7 8.6,13 6.8,13 C5.2,13 4,12.1 4,10.9 C4,9.6 5.4,8.7 7,8.7 C7.6,8.7 8.1,8.8 8.6,9.1 Z"));
    private static readonly Geometry ImageGeometry = CreateImageGeometry();
    private static readonly Geometry UnknownGeometry = Freeze(Geometry.Parse("M4,3 L12,3 L12,13 L4,13 Z"));

    public object Convert(object? value, Type targetType, object? p, CultureInfo c)
        => value is MediaKind kind
            ? kind switch
            {
                MediaKind.Audio => AudioGeometry,
                MediaKind.Image => ImageGeometry,
                MediaKind.Video => VideoGeometry,
                _ => UnknownGeometry
            }
            : UnknownGeometry;

    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => Binding.DoNothing;

    private static Geometry CreateVideoGeometry()
    {
        var group = new GeometryGroup { FillRule = FillRule.EvenOdd };
        group.Children.Add(new RectangleGeometry(new Rect(2, 3.5, 12, 9)));
        group.Children.Add(new RectangleGeometry(new Rect(3.2, 5, 1, 1)));
        group.Children.Add(new RectangleGeometry(new Rect(3.2, 7.5, 1, 1)));
        group.Children.Add(new RectangleGeometry(new Rect(3.2, 10, 1, 1)));
        group.Children.Add(new RectangleGeometry(new Rect(11.8, 5, 1, 1)));
        group.Children.Add(new RectangleGeometry(new Rect(11.8, 7.5, 1, 1)));
        group.Children.Add(new RectangleGeometry(new Rect(11.8, 10, 1, 1)));
        group.Children.Add(Geometry.Parse("M6.5,6 L6.5,10 L10,8 Z"));
        group.Freeze();
        return group;
    }

    private static Geometry CreateImageGeometry()
    {
        var group = new GeometryGroup { FillRule = FillRule.EvenOdd };
        group.Children.Add(new RectangleGeometry(new Rect(2.5, 3.5, 11, 9)));
        group.Children.Add(new RectangleGeometry(new Rect(4, 5, 8, 5.8)));
        group.Children.Add(Geometry.Parse("M4,10.8 L6.8,8 L8.8,10 L10,8.8 L12,10.8 Z"));
        group.Children.Add(new EllipseGeometry(new Point(10.3, 6.5), 0.9, 0.9));
        group.Freeze();
        return group;
    }

    private static Geometry Freeze(Geometry geometry)
    {
        geometry.Freeze();
        return geometry;
    }
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

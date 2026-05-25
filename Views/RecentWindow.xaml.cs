using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using DenoPlayer.Models;
using DenoPlayer.ViewModels;

namespace DenoPlayer.Views;

public partial class RecentWindow : Window
{
    public bool IsShown { get; private set; }
    public event Action<bool>? ShownChanged;
    public new MainViewModel? DataContext
    {
        get => base.DataContext as MainViewModel;
        set => base.DataContext = value;
    }

    public RecentWindow()
    {
        InitializeComponent();
        Width = 340;
    }

    public void ShowSlide()
    {
        if (IsShown) return;
        IsShown = true;
        ShownChanged?.Invoke(true);
        var slide = new DoubleAnimation
        {
            To = 0,
            Duration = TimeSpan.FromMilliseconds(100),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        SlideTx.BeginAnimation(TranslateTransform.XProperty, slide);
    }

    public void HideSlide()
    {
        if (!IsShown) return;
        IsShown = false;
        ShownChanged?.Invoke(false);
        var slide = new DoubleAnimation
        {
            To = -Width,
            Duration = TimeSpan.FromMilliseconds(100),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        SlideTx.BeginAnimation(TranslateTransform.XProperty, slide);
    }

    private void OnPanelEnter(object sender, MouseEventArgs e) { }
    private void OnPanelLeave(object sender, MouseEventArgs e) => HideSlide();

    private void OnItemClicked(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject d)
        {
            var lbi = VisualSearch.FindAncestor<ListBoxItem>(d);
            if (lbi?.Content is RecentItem ri)
            {
                DataContext?.OpenPath(ri.FullPath);
                e.Handled = true;
            }
        }
    }

    private void OnClearAll(object? sender, RoutedEventArgs e)
    {
        DataContext?.Recents.Clear();
        if (DataContext?.Settings is { } s) s.RecentFiles = new List<string>();
    }
}

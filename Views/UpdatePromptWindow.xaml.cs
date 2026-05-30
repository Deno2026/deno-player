using System.Windows;
using System.Windows.Input;

namespace DenoVideoPlayer.Views;

public partial class UpdatePromptWindow : Window
{
    public UpdatePromptWindow(
        string title,
        string message,
        string updateText,
        string cancelText)
    {
        InitializeComponent();
        Title = title;
        TitleText.Text = title;
        MessageText.Text = message;
        UpdateButton.Content = updateText;
        CancelButton.Content = cancelText;
    }

    private void OnUpdateClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void OnDragHeader(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }
}

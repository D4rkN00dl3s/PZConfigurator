using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace PZConfigurator.Views;

/// <summary>Minimal modal "discard unsaved changes?" dialog.</summary>
public static class ConfirmWindow
{
    public static async Task<bool> ShowAsync(Window owner)
    {
        var result = new TaskCompletionSource<bool>();
        var dlg = new Window
        {
            Title = "Unsaved changes",
            Width = 360,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
        };

        var discard = new Button { Content = "Discard changes", MinWidth = 110 };
        var cancel = new Button { Content = "Cancel", MinWidth = 80 };
        discard.Click += (_, _) => { result.SetResult(true); dlg.Close(); };
        cancel.Click += (_, _) => { result.SetResult(false); dlg.Close(); };
        dlg.Closed += (_, _) => result.TrySetResult(false);

        dlg.Content = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 12,
            Children =
            {
                new TextBlock
                {
                    Text = "You have unsaved changes. Discard and close anyway?",
                    TextWrapping = TextWrapping.Wrap,
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { discard, cancel },
                },
            },
        };

        await dlg.ShowDialog(owner);
        return result.Task.Result;
    }
}

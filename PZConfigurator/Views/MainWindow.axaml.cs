using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PZConfigurator.ViewModels;

namespace PZConfigurator.Views;

public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    private async void Browse_Click(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select the Zomboid Server folder",
            AllowMultiple = false,
        });
        if (folders.Count > 0 && DataContext is MainViewModel vm)
            _ = vm.SetFolder(folders[0].Path.LocalPath); // fire-and-forget discover; errors land in vm.Status
    }

    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        if (DataContext is MainViewModel vm && vm.IsDirty)
        {
            e.Cancel = true;
            if (await ConfirmWindow.ShowAsync(this))
            {
                // clear dirty state so the re-entrant Close isn't cancelled again
                vm.IsDirty = false;
                vm.Title = "PZConfigurator";
                Close();
            }
        }
        base.OnClosing(e);
    }
}

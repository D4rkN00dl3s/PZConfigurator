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
            vm.SetFolder(folders[0].Path.LocalPath);
    }

    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        if (DataContext is MainViewModel vm && vm.IsDirty)
        {
            e.Cancel = true;
            if (await ConfirmWindow.ShowAsync(this))
                Close(); // IsDirty stays true but nobody reads it after close
        }
        base.OnClosing(e);
    }
}

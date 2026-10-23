using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PZConfigurator.ViewModels;

namespace PZConfigurator.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        KeyDown += MainWindow_KeyDown;
        var search = this.FindControl<TextBox>("SearchBox");
        if (search != null) search.KeyDown += SearchBox_KeyDown;
    }

    // Ctrl+S = Save, Ctrl+F = focus search
    private void MainWindow_KeyDown(object? sender, KeyEventArgs e)
    {
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control)) return;
        if (e.Key == Key.S && DataContext is MainViewModel vm)
        {
            if (vm.IsDirty) vm.SaveCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.F)
        {
            this.FindControl<TextBox>("SearchBox")?.Focus();
            e.Handled = true;
        }
    }

    private void SearchBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || DataContext is not MainViewModel vm) return;
        vm.SearchText = "";
        e.Handled = true;
    }

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

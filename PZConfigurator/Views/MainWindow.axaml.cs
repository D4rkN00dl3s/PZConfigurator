using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PZConfigurator.ViewModels;

namespace PZConfigurator.Views;

public partial class MainWindow : Window
{
    private readonly Config.AppSettings? _restored;

    public MainWindow()
    {
        InitializeComponent();
        _restored = Config.AppSettings.Load();
        // Bubble|handledEventsToo: Avalonia's keyboard navigation swallows Ctrl+Tab first
        AddHandler(KeyDownEvent, MainWindow_KeyDown, RoutingStrategies.Bubble, handledEventsToo: true);
        var search = this.FindControl<TextBox>("SearchBox");
        if (search != null) search.KeyDown += SearchBox_KeyDown;
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        // restore window geometry + last folder/server; the VM's ctor discover on the
        // default folder is harmless (fast local scan) and SetFolder below re-runs it
        // keyboard shortcuts need a focused element; search box is a useful default
        this.FindControl<TextBox>("SearchBox")?.Focus();
        var s = _restored;
        if (s != null)
        {
            var bounds = Screens.All.Select(sc => sc.Bounds).ToList();
            if (s.X.HasValue && s.Y.HasValue && s.Width is > 300 && s.Height is > 200
                && bounds.Any(b => b.Contains(new PixelPoint(s.X.Value + 10, s.Y.Value + 10))))
            {
                Position = new PixelPoint(s.X.Value, s.Y.Value);
                Width = s.Width.Value;
                Height = s.Height.Value;
                ClientSize = new Avalonia.Size(s.Width.Value, s.Height.Value);
            }
            if (s.Maximized) WindowState = WindowState.Maximized;
        }

        _ = RestoreSelectionAsync(s);
    }

    private async Task RestoreSelectionAsync(Config.AppSettings? s)
    {
        if (DataContext is not MainViewModel vm) return;
        if (s?.Folder is string f && f != vm.Folder)
        {
            vm.Folder = f;
            await vm.SetFolder(vm.Folder); // re-discover in the restored folder
        }
        if (s?.Server is string server && vm.Servers.Contains(server))
            vm.SelectedServer = server; // replaces the auto-selected first entry
    }

    private void PersistSettings()
    {
        if (DataContext is not MainViewModel vm) return;
        Config.AppSettings.Save(new Config.AppSettings
        {
            Folder = vm.Folder,
            Server = vm.LoadedServer ?? vm.SelectedServer,
            X = WindowState == WindowState.Maximized ? null : Position.X,
            Y = WindowState == WindowState.Maximized ? null : Position.Y,
            Width = Width,
            Height = Height,
            Maximized = WindowState == WindowState.Maximized,
        });
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
        else if (e.Key == Key.Tab && DataContext is MainViewModel tab)
        {
            tab.SelectedTabIndex = (tab.SelectedTabIndex + 1) % 2;
            e.Handled = true;
        }
    }

    private void SearchBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || DataContext is not MainViewModel vm) return;
        vm.SearchText = "";
        e.Handled = true;
    }

    // double-tap anywhere on a boolean row toggles it (wired in the BoolSettingVM template)
    private void BoolRow_DoubleTapped(object? sender, TappedEventArgs e)
    {
        // rapid presses on the glyph itself are the CheckBox's own click toggles; leave them alone
        if (e.Source is CheckBox) return;
        if (sender is Border { DataContext: BoolSettingVM b }) b.IsChecked = !b.IsChecked;
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
        PersistSettings();
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

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using PZConfigurator.ViewModels;
using PZConfigurator.Views;

namespace PZConfigurator;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow();
            desktop.MainWindow = window;
            // ViewModel asks the window's confirm dialog before discarding unsaved edits
            window.DataContext = new MainViewModel(() => ConfirmWindow.ShowAsync(window));
        }

        base.OnFrameworkInitializationCompleted();
    }
}
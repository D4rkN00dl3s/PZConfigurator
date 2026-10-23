using Avalonia;

namespace PZConfigurator.Tests;

/// <summary>xUnit tests touch Avalonia services (asset loader) without a running app;
/// initialize the app once headlessly.</summary>
public static class AvaloniaTestSetup
{
    static bool _done;

    public static void Ensure()
    {
        if (_done) return;
        _done = true;
        AppBuilder.Configure<App>().UsePlatformDetect().SetupWithoutStarting();
    }
}

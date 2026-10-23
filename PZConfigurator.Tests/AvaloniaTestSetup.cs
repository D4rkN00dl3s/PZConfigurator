using Avalonia;
using Avalonia.Headless;

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
        // headless: no window/display needed (Linux CI runners have no X server)
        AppBuilder.Configure<App>()
            .UseHeadless(new Avalonia.Headless.AvaloniaHeadlessPlatformOptions())
            .SetupWithoutStarting();
    }
}

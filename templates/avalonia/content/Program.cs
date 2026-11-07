using Avalonia;
using Routeables.Avalonia;

namespace RouteablesApp;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            // The pages are registered for you (the generator finds [Routeable] and the views); the first page is the one the region of
            // MainWindow.axaml opens. To register by hand too, pass a block: r => r.RegisterRoute<...>(...).
            .UseRouteables(view: lifetime => new MainWindow())
            .StartWithClassicDesktopLifetime(args);
}

using Avalonia;
using Routeables.Avalonia;
using Routeables.Generated;

namespace RouteablesApp;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .UseRouteables(
                r => r.AddGeneratedRoutes(),   // the first page is the one the region of MainWindow.axaml opens
                view: lifetime => new MainWindow())
            .StartWithClassicDesktopLifetime(args);
}

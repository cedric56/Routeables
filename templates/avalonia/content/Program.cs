using Avalonia;
using Routeables.Abstraction;
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
                r => { r.AddGeneratedRoutes(); r.AddHome("/hello/World"); },   // the first page
                view: lifetime => new MainWindow())
            .StartWithClassicDesktopLifetime(args);
}

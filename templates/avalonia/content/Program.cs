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
            .UseRouteables<MainViewModel>(
                r => r.AddGeneratedRoutes(),   // the first page is the one MainViewModel opens (MainWindow.axaml.cs)
                view: lifetime => new MainWindow())
            .StartWithClassicDesktopLifetime(args);
}

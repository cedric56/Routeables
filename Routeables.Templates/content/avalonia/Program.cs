using Avalonia;

namespace RouteablesApp;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Also used by the visual designer.
    public static AppBuilder BuildAvaloniaApp() => App.CreateApp()
        .UsePlatformDetect()
        .WithInterFont()
        .LogToTrace();
}

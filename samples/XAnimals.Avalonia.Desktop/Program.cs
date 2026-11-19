using Avalonia;
using Routeables.Avalonia;
using Routeables.Native;
using System;

namespace XAnimals.Avalonia.Desktop;

class Program
{
    // Initialization code. Do not use Avalonia or third-party APIs, or code that relies on a SynchronizationContext, before AppMain is called:
    // nothing is initialized yet.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Avalonia configuration, do not remove (the visual designer uses it too).
    public static AppBuilder BuildAvaloniaApp()
        => App.CreateApp()
            //(new Reshapes.Avalonia.Fluent.ReshapeMessageBoxService())
            //.AfterSetup(builder =>
            //{
            //    var app = builder.Instance;
            //    app?.Styles.Add(new FluentAvaloniaTheme());
            //})
            .UsePlatformDetect()
            .WithAppNotifications(new AppNotificationOptions())
            .WithInterFont()
            .LogToTrace();

}

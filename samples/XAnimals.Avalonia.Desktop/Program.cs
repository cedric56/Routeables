using Avalonia;
using Routeables.Avalonia;
using Routeables.Native;
using System;

namespace XAnimals.Avalonia.Desktop;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Avalonia configuration, don't remove; also used by visual designer.
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

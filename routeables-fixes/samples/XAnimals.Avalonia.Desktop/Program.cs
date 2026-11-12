using Avalonia;
using Avalonia.Labs.Notifications;
using System;
using System.Diagnostics;

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
    {
        var builder = App.CreateApp()
            //(new Reshapes.Avalonia.Fluent.ReshapeMessageBoxService())
            //.AfterSetup(builder =>
            //{
            //    var app = builder.Instance;
            //    app?.Styles.Add(new FluentAvaloniaTheme());
            //})
            .UsePlatformDetect();

        // On Linux, Avalonia.Labs.Notifications 12.1.0 is built against Tmds.DBus.Protocol 0.92 (DBusConnection.AsConnection), which
        // 0.93 removed, and Avalonia 12.1 brings a later one: its notification manager cannot be created (MissingMethodException).
        // The application then runs without system notifications rather than not at all.
        try
        {
            builder = builder.WithAppNotifications(new AppNotificationOptions()
            {
                //AppIcon = Path.GetDirectoryName(Process.GetCurrentProcess()?.MainModule?.FileName) + "/avalonia-32.png",
                AppName = "Avalonia.Labs",
                //AppUserModelId = "com.Avalonia.Labs.Catalog",
            });
        }
        catch (MissingMethodException ex) when (OperatingSystem.IsLinux())
        {
            Trace.WriteLine($"System notifications are off: {ex.Message}");
        }

        return builder
            .WithInterFont()
            .LogToTrace();
    }

}

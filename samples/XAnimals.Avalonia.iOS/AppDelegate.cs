using Avalonia;
using Avalonia.iOS;
using Foundation;
using Routeables.Avalonia;
using AvaloniaApplication = Avalonia.Application;

namespace XAnimals.Avalonia.iOS;

// The UIApplicationDelegate of the application: it launches the UI and listens to the application events from iOS.
[Register("AppDelegate")]
public partial class AppDelegate : AvaloniaAppDelegate<AvaloniaApplication>
{
    protected override AppBuilder CreateAppBuilder()
    {
        return App.CreateApp()
            .UseiOS()
            .WithAppNotifications();
    }
}

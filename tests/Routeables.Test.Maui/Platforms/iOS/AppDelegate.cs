using Foundation;

namespace Routeables.Test.Maui
{
    /// <summary>
    /// The application delegate on iOS and MacCatalyst: the platform asks it for the MAUI application, and it gives the one the
    /// tests are built on.
    /// </summary>
    [Register("AppDelegate")]
    public class AppDelegate : MauiUIApplicationDelegate
    {
        /// <summary>Gives the test application to the platform: the same one <see cref="MauiProgram"/> builds for every target.</summary>
        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    }
}
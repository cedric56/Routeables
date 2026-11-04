using Foundation;

namespace Routeables.Test.Maui
{
    /// <summary>
    /// Represents the iOS application delegate responsible for managing the application lifecycle.
    /// </summary>
    [Register("AppDelegate")]
    public class AppDelegate : MauiUIApplicationDelegate
    {
        /// <summary>
        /// Creates and configures the .NET MAUI application instance.
        /// </summary>
        /// <returns>
        /// The configured <see cref="MauiApp"/> instance.
        /// </returns>
        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    }
}
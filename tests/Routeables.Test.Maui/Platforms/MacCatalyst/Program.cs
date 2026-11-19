using UIKit;

namespace Routeables.Test.Maui
{
    /// <summary>
    /// Provides the main entry point for the iOS application.
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Starts the iOS application and initializes its application delegate.
        /// </summary>
        /// <param name="args">
        /// An array of command-line arguments passed to the application.
        /// </param>
        static void Main(string[] args)
        {
            UIApplication.Main(args, null, typeof(AppDelegate));
        }
    }
}
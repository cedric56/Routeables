using Android.App;
using Android.Content.PM;

namespace Routeables.Test.Maui
{
    // The runner starts "com.routeables.test.maui/.MainActivity". Without an explicit name Android hashes the class name, and the launch fails
    // with "class not found" (result code -92) before any test runs.
    [Activity(Name = "com.routeables.test.maui.MainActivity", Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
    }
}

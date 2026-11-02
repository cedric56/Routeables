using Android.App;
using Android.Content.PM;

namespace Routeables.Test.Maui
{
    // The runner starts "com.routeables.test.maui/.MainActivity": without a name Android knows the class under a hashed one, and the launch
    // fails with "class not found" (result code -92) before a single test has run.
    [Activity(Name = "com.routeables.test.maui.MainActivity", Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
    }
}

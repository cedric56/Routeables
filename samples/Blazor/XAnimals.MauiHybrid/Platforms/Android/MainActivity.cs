using Android.App;
using Android.Content.PM;

namespace XAnimals.MauiHybrid
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        // A tap on a system notification (Routeables.Native) comes back with the intent that starts or reaches the activity.
        protected override void OnCreate(Android.OS.Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Routeables.Native.SystemNotificationCenter.HandleIntent(Intent);
        }

        protected override void OnNewIntent(Android.Content.Intent? intent)
        {
            base.OnNewIntent(intent);
            Routeables.Native.SystemNotificationCenter.HandleIntent(intent);
        }
    }
}

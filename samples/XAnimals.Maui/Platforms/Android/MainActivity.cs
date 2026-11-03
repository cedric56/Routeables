using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;

namespace XAnimals.Maui
{
    [IntentFilter([Intent.ActionView], Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable], DataScheme = "xanimals")]
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        private const string State = "routeables.history";

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            LinkInbox.Instance.Restore(savedInstanceState?.GetString(State));
            LinkInbox.Instance.Push(Intent?.Data is { } data ? new Uri(data.ToString()!) : null);
        }

        protected override void OnNewIntent(Intent? intent)
        {
            base.OnNewIntent(intent);
            LinkInbox.Instance.Push(intent?.Data is { } data ? new Uri(data.ToString()!) : null);
        }

        protected override void OnSaveInstanceState(Bundle outState)
        {
            base.OnSaveInstanceState(outState);
            outState.PutString(State, LinkInbox.Instance.Save());
        }
    }
}

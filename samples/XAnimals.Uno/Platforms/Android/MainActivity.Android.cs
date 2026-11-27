using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using XAnimals.Uno;

namespace XAnimals.Droid;

[IntentFilter([Intent.ActionView], Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable], DataScheme = "xanimals")]
[Activity(
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = global::Uno.UI.ActivityHelper.AllConfigChanges,
    WindowSoftInputMode = SoftInput.AdjustNothing | SoftInput.StateHidden,
    Exported = true
)]
public class MainActivity : Microsoft.UI.Xaml.ApplicationActivity
{
    private const string State = "routeables.history";

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        global::AndroidX.Core.SplashScreen.SplashScreen.InstallSplashScreen(this);

        base.OnCreate(savedInstanceState);

        // A tap on a system notification comes back as NotificationCompleted.
        Routeables.Native.SystemNotificationCenter.HandleIntent(Intent);

        // The pages the system killed with the process come back, then the link that started the application (if any) is followed.
        LinkInbox.Instance.Restore(savedInstanceState?.GetString(State));
        LinkInbox.Instance.Push(Intent?.Data is { } data ? new Uri(data.ToString()!) : null);
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        Routeables.Native.SystemNotificationCenter.HandleIntent(intent);
        LinkInbox.Instance.Push(intent?.Data is { } data ? new Uri(data.ToString()!) : null);
    }

    protected override void OnSaveInstanceState(Bundle outState)
    {
        base.OnSaveInstanceState(outState);
        outState.PutString(State, LinkInbox.Instance.Save());
    }

}

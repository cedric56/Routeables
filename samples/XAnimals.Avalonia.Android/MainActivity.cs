using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using Avalonia.Labs.Notifications;
using System;
using XAnimals.Avalonia;
using Application = Avalonia.Application;

namespace XAnimals.Avalonia.Android;

[Activity(
    Label = "XAnimals.Avalonia.Android",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
[IntentFilter(
    [Intent.ActionView],
    Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable],
    DataScheme = "xanimals")]
public class MainActivity : AvaloniaMainActivity
{
    private const string State = "routeables.history";

    public event EventHandler<Intent?>? OnActivityIntent;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        NativeNotificationManager.Current?.SetPermissionActivity(this);

        // The pages the system killed with the process come back, then the link that started the application (if any) is followed.
        LinkInbox.Instance.Restore(savedInstanceState?.GetString(State));
        LinkInbox.Instance.Push(Intent?.Data is { } data ? new Uri(data.ToString()!) : null);
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        OnActivityIntent?.Invoke(this, intent);
        LinkInbox.Instance.Push(intent?.Data is { } data ? new Uri(data.ToString()!) : null);
    }

    protected override void OnSaveInstanceState(Bundle outState)
    {
        base.OnSaveInstanceState(outState);
        outState.PutString(State, LinkInbox.Instance.Save());
    }
}

[Application]
public class MainApplication : AvaloniaAndroidApplication<Application>
{
    protected MainApplication(nint javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
    {
    }

    protected override AppBuilder CreateAppBuilder()
    {
        return App.CreateApp()
            .UseAndroid()
            .WithAppNotifications(this);
    }
}

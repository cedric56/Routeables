using Android.App;
using Android.Content.PM;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using Application = Avalonia.Application;

namespace XAnimals.Avalonia.Android;

[Activity(
    Label = "XAnimals.Avalonia.Android",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity
{

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
            .UseAndroid();
    }
}

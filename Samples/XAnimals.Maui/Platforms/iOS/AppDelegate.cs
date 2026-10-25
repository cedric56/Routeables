using Foundation;
using UIKit;

namespace XAnimals.Maui
{
    [Register("AppDelegate")]
    public class AppDelegate : MauiUIApplicationDelegate
    {
        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

        // A link opened while the application runs (without scenes; with them SceneDelegate receives it).
        public override bool OpenUrl(UIApplication application, NSUrl url, NSDictionary options)
        {
            LinkInbox.Instance.Push(url.AbsoluteUrl is { } absolute && absolute.AbsoluteString is not null ? new Uri(absolute.AbsoluteString) : null);
            return true;
        }
    }
}

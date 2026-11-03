using Foundation;
using UIKit;

namespace XAnimals.Maui
{
    [Register("SceneDelegate")]
    public class SceneDelegate : MauiUISceneDelegate
    {
        // The link that starts the application, and the ones opened while it runs.
        public override void WillConnect(UIScene scene, UISceneSession session, UISceneConnectionOptions connectionOptions)
        {
            base.WillConnect(scene, session, connectionOptions);
            Push(connectionOptions.UrlContexts);
        }

        // The protocol method of UIWindowSceneDelegate: MauiUISceneDelegate does not declare it, so it is exported, not overridden.
        [Export("scene:openURLContexts:")]
        public void OpenUrlContexts(UIScene scene, NSSet<UIOpenUrlContext> urlContexts) => Push(urlContexts);

        private static void Push(NSSet<UIOpenUrlContext>? contexts)
        {
            foreach (UIOpenUrlContext context in contexts?.ToArray<UIOpenUrlContext>() ?? [])
                LinkInbox.Instance.Push(context.Url.AbsoluteUrl is { } absolute && absolute.AbsoluteString is not null ? new Uri(absolute.AbsoluteString) : null);
        }
    }
}

using Avalonia;
using Routeables.Native;

namespace Routeables.Avalonia;

/// <summary>Turns on the system notifications of <see cref="Routeables.Services.IRouteableNotificationService"/> on an Avalonia application.</summary>
public static class AppNotificationsExtensions
{
    /// <summary>
    /// Turns on the system notifications of <c>IRouteableNotificationService</c> (<c>CreateNotificationAsync</c>, <c>ActiveNotifications</c>,
    /// <c>CloseAll</c>, <c>NotificationCompleted</c>, <c>InitializeAsync</c>): the notification center of Android and iOS, through
    /// <c>Routeables.Native</c>, and on the desktop Linux (D-Bus) and Windows (the notification area). Separate from <c>UseRouteables</c>: without this call the service creates none. macOS and the browser have no
    /// system notification: <c>CreateNotificationAsync</c> returns <see langword="null"/> there. On Android the main activity passes its
    /// intents to <c>SystemNotificationCenter.HandleIntent</c> (see the guide), and the manifest declares <c>POST_NOTIFICATIONS</c>.
    /// <code>
    /// App.CreateApp().UsePlatformDetect().WithAppNotifications();
    /// </code>
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <param name="options">The options (<c>ClearOnAppClose</c>, <c>ChannelName</c>); the defaults when <see langword="null"/>.</param>
    /// <returns>The same <paramref name="builder"/>, for chaining.</returns>
    public static AppBuilder WithAppNotifications(this AppBuilder builder, AppNotificationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        SystemNotificationCenter.Current.Configure(options ?? new AppNotificationOptions());
        return builder;
    }
}

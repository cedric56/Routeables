using Routeables.Abstraction;
using Routeables.Services;

namespace Routeables.Maui.Services;

/// <summary>
/// Windows, Android, iOS and Mac Catalyst use their own toast (<c>RouteableToastHelper</c>, cards drawn with the platform's views, see
/// https://github.com/gurrenm3/Toast-Notifications-with-.Net-Maui for the first two); the plain <c>net10.0</c> target uses <see cref="RouteableToastOverlay"/>.
/// </summary>
internal class RouteableNotificationService(
#if !WINDOWS && !ANDROID && !IOS && !MACCATALYST
    IRouteableDialogWindowContext context
#endif
    ) : RouteableNotificationServiceBase(new RouteableSystemNotifications())
{
    /// <summary>
    /// Displays a transient notification with a type-colored icon, optionally auto-dismissing
    /// after <paramref name="expiration"/> and invoking callbacks on tap or close.
    /// </summary>
    /// <param name="title">The notification title.</param>
    /// <param name="message">The notification message.</param>
    /// <param name="type">The notification type, used to color the icon. Defaults to <see cref="RouteableNotificationType.Information"/>.</param>
    /// <param name="expiration">Optional duration after which the notification is automatically dismissed if not already closed.</param>
    /// <param name="onClick">Optional callback invoked when the notification is tapped.</param>
    /// <param name="onClose">Optional callback invoked once the notification has been dismissed.</param>
    public override void Show(string title, string message, RouteableNotificationType type = RouteableNotificationType.Information, TimeSpan? expiration = null, Action? onClick = null, Action? onClose = null)
    {
#if WINDOWS || ANDROID || IOS || MACCATALYST
        RouteableDefaultNotifications.Show(null!, title, message, type, expiration, onClick, onClose);   // the platform's own toast needs no page
#else
        RouteableDefaultNotifications.Show(context, title, message, type, expiration, onClick, onClose);
#endif
    }
}

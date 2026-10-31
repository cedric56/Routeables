using Routeables.Services;

namespace Routeables.Maui.Services;

/// <summary>
/// The notification of Routeables itself, for a replacement of <see cref="IRouteableNotificationService"/> to fall back on when its own cannot be shown
/// (the CommunityToolkit snackbar of the Reshapes, for instance): the toast of the platform on Windows, Android, iOS and Mac Catalyst, a card over the
/// current page on the plain <c>net10.0</c> target.
/// </summary>
public static class RouteableDefaultNotifications
{
    /// <summary>Displays a notification the way <c>UseRouteables</c> does when nothing replaces the notification service.</summary>
    /// <param name="context">The window context, which gives the current page of the plain <c>net10.0</c> target.</param>
    /// <param name="title">The notification title.</param>
    /// <param name="message">The notification message.</param>
    /// <param name="type">The notification type.</param>
    /// <param name="expiration">Optional duration after which the notification is dismissed.</param>
    /// <param name="onClick">Optional callback invoked when the notification is tapped.</param>
    /// <param name="onClose">Optional callback invoked once the notification has been dismissed.</param>
    public static void Show(IRouteableDialogWindowContext context, string title, string message, RouteableNotificationType type = RouteableNotificationType.Information,
        TimeSpan? expiration = null, Action? onClick = null, Action? onClose = null)
    {
#if WINDOWS || ANDROID || IOS || MACCATALYST
        RouteableToastHelper.Show(title, message, type, expiration, onClick, onClose);
#else
        ArgumentNullException.ThrowIfNull(context);
        var page = context.CurrentPage
           ?? throw new InvalidOperationException(
               "No active Page to host the notification.");

        page.Dispatcher.Dispatch(() => RouteableToastOverlay.Show(page, title, message, type, expiration, onClick, onClose));
#endif
    }
}

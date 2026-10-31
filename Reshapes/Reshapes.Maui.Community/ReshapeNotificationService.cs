using Routeables.Abstraction;
using Routeables.Maui.Services;
using CommunityToolkit.Maui.Alerts;
using Routeables.Services;

namespace Reshapes.Maui.CommunityToolkit;

/// <summary>
/// https://github.com/gurrenm3/Toast-Notifications-with-.Net-Maui
/// </summary>
public class ReshapeNotificationService(IRouteableDialogWindowContext context, RouteableSystemNotifications? system = null) : RouteableNotificationServiceBase(system)
{
    /// <summary>
    /// Displays a transient popup notification with a type-colored icon, optionally auto-dismissing
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
        // No page to host the snackbar: Routeables' own notification is shown instead.
        if (context.CurrentPage is not { } page)
        {
            ShowDefault(title, message, type, expiration, onClick, onClose);
            return;
        }

        _ = DisplayAsync(page, title, message, type, expiration, onClick, onClose);
    }

    // Not async void: a snackbar that cannot be shown must not end the application. It is reported, and the notification
    // of Routeables takes its place, so the user still sees it.
    private async Task DisplayAsync(Page page, string title, string message, RouteableNotificationType type, TimeSpan? expiration, Action? onClick, Action? onClose)
    {
        try { await page.DisplaySnackbar(message, onClick, duration: expiration); }
        catch (Exception ex)
        {
            RouteableErrors.Report<ReshapeNotificationService>(null, ex);
            page.Dispatcher.Dispatch(() => ShowDefault(title, message, type, expiration, onClick, onClose));
        }
    }

    private void ShowDefault(string title, string message, RouteableNotificationType type, TimeSpan? expiration, Action? onClick, Action? onClose)
    {
        try { RouteableDefaultNotifications.Show(context, title, message, type, expiration, onClick, onClose); }
        catch (Exception ex) { RouteableErrors.Report<ReshapeNotificationService>(null, ex); }
    }
}

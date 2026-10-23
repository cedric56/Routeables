using Routeables.Abstraction;
using Routeables.Maui.Services;
using CommunityToolkit.Maui.Alerts;
using Routeables.Services;

namespace Reshapes.Maui.CommunityToolkit;

/// <summary>
/// https://github.com/gurrenm3/Toast-Notifications-with-.Net-Maui
/// </summary>
public class ReshapeNotificationService(IRouteableDialogWindowContext context) : IRouteableNotificationService
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
    public void Show(string title, string message, RouteableNotificationType type = RouteableNotificationType.Information, TimeSpan? expiration = null, Action? onClick = null, Action? onClose = null)
    {
        var page = context.CurrentPage
           ?? throw new InvalidOperationException(
               "No active Page to host the content dialog popup.");
        _ = DisplayAsync(page, message, onClick, expiration);
    }

    // Not async void: a snackbar that cannot be shown must not end the application.
    private static async Task DisplayAsync(Page page, string message, Action? onClick, TimeSpan? expiration)
    {
        try { await page.DisplaySnackbar(message, onClick, duration: expiration); }
        catch (Exception ex) { RouteableErrors.Report(null, "ReshapeNotificationService.Show", ex); }
    }
}

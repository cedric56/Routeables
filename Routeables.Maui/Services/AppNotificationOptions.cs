namespace Routeables.Maui.Services;

/// <summary>
/// Options of the system notifications, given to <see cref="Extensions.WithAppNotifications"/> like the <c>AppNotificationOptions</c> of
/// Avalonia.Labs.Notifications (its <c>ClearOnAppClose</c> is here too).
/// </summary>
public sealed class AppNotificationOptions
{
    /// <summary>Whether the notifications still displayed are withdrawn when the application ends. <see langword="false"/> by default.</summary>
    public bool ClearOnAppClose { get; set; }

    /// <summary>The name of the notification channel the user sees in the settings of Android 8 and later.</summary>
    public string ChannelName { get; set; } = "Notifications";
}

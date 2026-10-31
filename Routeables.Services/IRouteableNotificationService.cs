namespace Routeables.Services;

/// <summary>
/// The severity/intent of a notification, typically used by the platform
/// adapter to pick an icon and accent color (e.g. red for
/// <see cref="Error"/>, green for <see cref="Success"/>).
/// </summary>
public enum RouteableNotificationType
{
    /// <summary>A neutral, informational notification.</summary>
    Information,

    /// <summary>A notification indicating an operation completed successfully.</summary>
    Success,

    /// <summary>A notification highlighting something that needs attention but isn't an error.</summary>
    Warning,

    /// <summary>A notification indicating an operation failed or something went wrong.</summary>
    Error
}

/// <summary>
/// Where an in-app (toast-style) notification should be anchored on screen.
/// Not all platform adapters may support every position — check the
/// concrete implementation for which positions are honored.
/// </summary>
public enum RouteableNotificationPosition
{
    /// <summary>Anchored to the top-left corner.</summary>
    TopLeft,

    /// <summary>Anchored to the top-right corner.</summary>
    TopRight,

    /// <summary>Anchored to the bottom-left corner.</summary>
    BottomLeft,

    /// <summary>Anchored to the bottom-right corner.</summary>
    BottomRight,

    /// <summary>Anchored to the top, horizontally centered.</summary>
    TopCenter,

    /// <summary>Anchored to the bottom, horizontally centered.</summary>
    BottomCenter
}

/// <summary>
/// Displays lightweight, non-modal notifications (toasts/banners), and optionally system ones, in a
/// platform-agnostic way, so ViewModels can surface transient feedback
/// without knowing whether the app is running on Avalonia, MAUI, or Blazor.
/// </summary>
public interface IRouteableNotificationService
{
    /// <summary>
    /// Shows a notification.
    /// </summary>
    /// <param name="title">The notification's title/heading.</param>
    /// <param name="message">The notification's body text.</param>
    /// <param name="type">
    /// The severity/intent of the notification, used to pick an icon and
    /// accent color. Defaults to <see cref="RouteableNotificationType.Information"/>.
    /// </param>
    /// <param name="expiration">
    /// How long the notification stays visible before auto-dismissing.
    /// If <see langword="null"/>, <see cref="RouteableNotificationDefaults.Expiration"/> (five seconds) is used, on every host.
    /// <see cref="RouteableNotificationDefaults.Never"/> keeps it until the user closes or taps it.
    /// </param>
    /// <param name="onClick">
    /// Invoked if the user taps/clicks the notification itself (not a
    /// specific button — this interface has no action-button parameters).
    /// </param>
    /// <param name="onClose">
    /// Invoked when the notification is dismissed, whether by the user,
    /// by <paramref name="expiration"/> elapsing, or programmatically by
    /// the platform adapter.
    /// </param>
    void Show(
        string title,
        string message,
        RouteableNotificationType type = RouteableNotificationType.Information,
        TimeSpan? expiration = null,
        Action? onClick = null,
        Action? onClose = null);

    // ---- system (OS notification-center) notifications -------------------------------------------------------------
    // Optional: only Avalonia implements them (Avalonia.Labs.Notifications). The defaults below say so (nothing active, nothing created), so an implementation
    // of the toasts above is complete without them.

    /// <summary>
    /// Gets the system-level (OS notification-center) notifications currently active (shown and not yet closed),
    /// keyed by <see cref="IRouteableSystemNotification.Id"/>. Empty when the host has no system notifications.
    /// </summary>
    IReadOnlyDictionary<uint, IRouteableSystemNotification> ActiveNotifications()
        => new Dictionary<uint, IRouteableSystemNotification>();

    /// <summary>
    /// Creates a new, not-yet-shown system notification. Call <see cref="IRouteableSystemNotification.ShowAsync"/> on the result to display it.
    /// </summary>
    /// <param name="category">
    /// The category to create the notification under. If <see langword="null"/>, the implementation uses a default category;
    /// otherwise the category must already be defined at app launch.
    /// </param>
    /// <returns>
    /// The new notification, or <see langword="null"/> if it couldn't be created (an unrecognized <paramref name="category"/>,
    /// the platform denies notification permission, or the host has no system notifications, which is the default).
    /// </returns>
    Task<IRouteableSystemNotification?> CreateNotificationAsync(string? category)
        => Task.FromResult<IRouteableSystemNotification?>(null);

    /// <summary>Closes every currently active system notification. A no-op by default.</summary>
    void CloseAll()
    {
    }

    /// <summary>
    /// Raised when the user interacts with or dismisses a system notification — see <see cref="RouteableSystemNotificationEventArgs"/>
    /// for what triggered it. Never raised by default.
    /// </summary>
    event EventHandler<RouteableSystemNotificationEventArgs>? NotificationCompleted
    {
        add { }
        remove { }
    }

    /// <summary>
    /// Performs any platform-specific setup needed before system notifications can be created (requesting OS permission,
    /// registering categories). A no-op by default.
    /// </summary>
    Task InitializeAsync() => Task.CompletedTask;
}

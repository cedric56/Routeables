using Routeables.Services;

namespace Routeables.Razor.Services;

/// <summary>
/// The base of a replacement of <see cref="IRouteableNotificationService"/>: it gives the system notifications of Routeables
/// (<c>WithAppNotifications</c>, the notification center of the browser), so that a replacement only writes what it changes, <see cref="Show"/>, usually the toast drawn
/// with a UI library. A class that does not derive from it has the defaults of the interface: no system notification.
/// </summary>
/// <param name="system">The system notifications of Routeables; none when <see langword="null"/>.</param>
public abstract class RouteableNotificationServiceBase(RouteableSystemNotifications? system = null) : IRouteableNotificationService
{
    /// <summary>
    /// Displays a transient notification with the specified title, message, type, expiration duration and interaction handlers.
    /// </summary>
    /// <param name="title">The notification title.</param>
    /// <param name="message">The notification message.</param>
    /// <param name="type">The type of notification.</param>
    /// <param name="expiration">Optional duration after which the notification is dismissed; <see langword="null"/> is the default of the implementation.</param>
    /// <param name="onClick">Optional callback invoked when the notification is clicked.</param>
    /// <param name="onClose">Optional callback invoked once the notification has been closed.</param>
    public abstract void Show(string title, string message, RouteableNotificationType type = RouteableNotificationType.Information,
        TimeSpan? expiration = null, Action? onClick = null, Action? onClose = null);

    /// <inheritdoc />
    public IReadOnlyDictionary<uint, IRouteableSystemNotification> ActiveNotifications()
        => system?.ActiveNotifications() ?? new Dictionary<uint, IRouteableSystemNotification>();

    /// <inheritdoc />
    public Task<IRouteableSystemNotification?> CreateNotificationAsync(string? category)
        => system?.CreateNotificationAsync(category) ?? Task.FromResult<IRouteableSystemNotification?>(null);

    /// <inheritdoc />
    public void CloseAll() => system?.CloseAll();

    /// <inheritdoc />
    public event EventHandler<RouteableSystemNotificationEventArgs>? NotificationCompleted
    {
        add { if (system is not null) system.NotificationCompleted += value; }
        remove { if (system is not null) system.NotificationCompleted -= value; }
    }

    /// <inheritdoc />
    public Task InitializeAsync() => system?.InitializeAsync() ?? Task.CompletedTask;
}

using Routeables.Services;

namespace Routeables.AspNetCore;

/// <summary>
/// The notifications of the web host: the page the visitor gets next shows the toasts. The system notifications are those of the browser: the
/// server queues them and that page hands them to the browser (<see cref="RouteableInteractions.TakeSystemNotificationsJson"/>); the visitor
/// must have allowed them. The page tells the server when the visitor clicks one or closes it, with a request, and
/// <see cref="IRouteableNotificationService.NotificationCompleted"/> is raised then (see <see cref="RouteableInteractions.CompleteSystemNotification"/>).
/// </summary>
public sealed class RouteableNotificationService(RouteableInteractions interactions) : IRouteableNotificationService
{
    /// <summary>
    /// Queues a toast that will be shown on the next page rendered for the visitor.
    /// </summary>
    public void Show(string title, string message, RouteableNotificationType type = RouteableNotificationType.Information, TimeSpan? expiration = null, Action? onClick = null, Action? onClose = null)
        => interactions.Notify(title, message, type, expiration, onClick, onClose);

    /// <inheritdoc />
    public IReadOnlyDictionary<uint, IRouteableSystemNotification> ActiveNotifications() => interactions.ActiveSystemNotifications;

    /// <inheritdoc />
    public Task<IRouteableSystemNotification?> CreateNotificationAsync(string? category)
        => Task.FromResult<IRouteableSystemNotification?>(interactions.CreateSystemNotification(category));

    /// <inheritdoc />
    public void CloseAll() => interactions.CloseAllSystemNotifications();

    /// <summary>
    /// Raised when the page reports that the visitor clicked a system notification or closed it
    /// (<see cref="RouteableInteractions.CompleteSystemNotification"/>); the report is a request that the page sends after the click.
    /// </summary>
    public event EventHandler<RouteableSystemNotificationEventArgs>? NotificationCompleted
    {
        add => interactions.SystemNotificationCompleted += value;
        remove => interactions.SystemNotificationCompleted -= value;
    }
}

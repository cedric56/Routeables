namespace Routeables.Services;

/// <summary>
/// Where the system notifications of a host come from: the notification center of the browser, or the one of the platform (Android, iOS,
/// Mac Catalyst, Windows). The notification services of a host ask it; a host that can have two (Blazor Hybrid) registers the one that works.
/// </summary>
public interface IRouteableSystemNotificationSource
{
    /// <summary>The system notifications that are active, by id.</summary>
    IReadOnlyDictionary<uint, IRouteableSystemNotification> ActiveNotifications();

    /// <summary>Creates a system notification, or <see langword="null"/> when there is none or the user denies it.</summary>
    Task<IRouteableSystemNotification?> CreateNotificationAsync(string? category);

    /// <summary>Closes every system notification that is active.</summary>
    void CloseAll();

    /// <summary>Raised when the user interacts with or dismisses a system notification.</summary>
    event EventHandler<RouteableSystemNotificationEventArgs>? NotificationCompleted;

    /// <summary>Prepares the system notifications (asks for the permission).</summary>
    Task InitializeAsync();
}

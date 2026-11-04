using Routeables.Services;

namespace Routeables.Maui.Services;

/// <summary>
/// The system notifications of Routeables (<c>WithAppNotifications</c>): Android, iOS, Mac Catalyst and Windows. A replacement of
/// <see cref="IRouteableNotificationService"/> that only redefines <c>Show</c> (the CommunityToolkit snackbars of the Reshapes) takes the system
/// members from here, registered by <c>UseRouteablesServices</c>.
/// </summary>
public sealed class RouteableSystemNotifications
{
    internal RouteableSystemNotifications()
    {
    }

    /// <summary>The system notifications that are active, by id.</summary>
    public IReadOnlyDictionary<uint, IRouteableSystemNotification> ActiveNotifications() => SystemNotificationCenter.Current.ActiveNotifications();

    /// <summary>Creates a system notification, or <see langword="null"/> when there is none on this platform or the user denies it.</summary>
    public Task<IRouteableSystemNotification?> CreateNotificationAsync(string? category) => SystemNotificationCenter.Current.CreateNotificationAsync(category);

    /// <summary>Closes every system notification that is active.</summary>
    public void CloseAll() => SystemNotificationCenter.Current.CloseAll();

    /// <summary>Raised when the user interacts with or dismisses a system notification.</summary>
    public event EventHandler<RouteableSystemNotificationEventArgs>? NotificationCompleted
    {
        add => SystemNotificationCenter.Current.NotificationCompleted += value;
        remove => SystemNotificationCenter.Current.NotificationCompleted -= value;
    }

    /// <summary>Prepares the system notifications (asks for the permission).</summary>
    public Task InitializeAsync() => SystemNotificationCenter.Current.InitializeAsync();
}

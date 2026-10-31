using Routeables.Services;

namespace Routeables.Razor.Services;

/// <summary>
/// The system notifications of Routeables (<c>WithAppNotifications</c>): the notification center of the browser. A replacement of
/// <see cref="IRouteableNotificationService"/> that only redefines <c>Show</c> (the MudBlazor snackbars of the Reshapes) takes the system
/// members from here, registered by <c>UseRouteablesServices</c>. Without <c>WithAppNotifications</c> it creates none.
/// </summary>
public sealed class RouteableSystemNotifications
{
    private readonly BrowserSystemNotifications? _system;

    internal RouteableSystemNotifications(BrowserSystemNotifications? system) => _system = system;

    /// <summary>The system notifications that are active, by id.</summary>
    public IReadOnlyDictionary<uint, IRouteableSystemNotification> ActiveNotifications()
        => _system?.ActiveNotifications() ?? new Dictionary<uint, IRouteableSystemNotification>();

    /// <summary>Creates a system notification, or <see langword="null"/> when there is none or the user denies it.</summary>
    public Task<IRouteableSystemNotification?> CreateNotificationAsync(string? category)
        => _system?.CreateNotificationAsync(category) ?? Task.FromResult<IRouteableSystemNotification?>(null);

    /// <summary>Closes every system notification that is active.</summary>
    public void CloseAll() => _system?.CloseAll();

    /// <summary>Raised when the user interacts with or dismisses a system notification.</summary>
    public event EventHandler<RouteableSystemNotificationEventArgs>? NotificationCompleted
    {
        add { if (_system is not null) _system.NotificationCompleted += value; }
        remove { if (_system is not null) _system.NotificationCompleted -= value; }
    }

    /// <summary>Prepares the system notifications (asks for the permission).</summary>
    public Task InitializeAsync() => _system?.InitializeAsync() ?? Task.CompletedTask;
}

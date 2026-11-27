using Routeables.Native;
using Routeables.Services;

namespace Routeables.Wpf.Services;

// The system notifications (the notification center of Windows) of IRouteableNotificationService, on Routeables.Native: the toasts of
// Windows 10 and 11. Without WithAppNotifications nothing is created.
internal sealed partial class RouteableNotificationService
{
    public IReadOnlyDictionary<uint, IRouteableSystemNotification> ActiveNotifications() => SystemNotificationCenter.Current.ActiveNotifications();

    public Task<IRouteableSystemNotification?> CreateNotificationAsync(string? category) => SystemNotificationCenter.Current.CreateNotificationAsync(category);

    public void CloseAll() => SystemNotificationCenter.Current.CloseAll();

    public Task InitializeAsync() => SystemNotificationCenter.Current.InitializeAsync();

    public event EventHandler<RouteableSystemNotificationEventArgs>? NotificationCompleted
    {
        add => SystemNotificationCenter.Current.NotificationCompleted += value;
        remove => SystemNotificationCenter.Current.NotificationCompleted -= value;
    }
}

using Routeables.Native;
using Routeables.Services;

namespace Routeables.Avalonia.Services;

// The system notifications (notification center of the OS) of IRouteableNotificationService, on Routeables.Native: Android and iOS have one.
// Elsewhere (desktop, the browser) nothing is active and nothing is created. Without WithAppNotifications nothing is created either.
internal partial class RouteableNotificationService
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

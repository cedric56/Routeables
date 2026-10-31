using Avalonia.Labs.Notifications;
using Routeables.Services;

namespace Routeables.Avalonia.Services;

// The system notifications (notification center of the OS) of IRouteableNotificationService, on Avalonia.Labs.Notifications.
// Without a native manager on the platform (the browser, for one) nothing is active and nothing is created.
internal partial class RouteableNotificationService
{
    private static INativeNotificationManager? Manager => NativeNotificationManager.Current;

    public IReadOnlyDictionary<uint, IRouteableSystemNotification> ActiveNotifications()
        => Manager?.ActiveNotifications.ToDictionary(p => p.Key, p => (IRouteableSystemNotification)new RouteableSystemNotification(p.Value))
           ?? new Dictionary<uint, IRouteableSystemNotification>();

    public Task<IRouteableSystemNotification?> CreateNotificationAsync(string? category)
    {
        if (Manager is not { } manager) return Task.FromResult<IRouteableSystemNotification?>(null);
        if (manager.CreateNotification(category) is { } created) return Task.FromResult<IRouteableSystemNotification?>(new RouteableSystemNotification(created));
        return Task.FromResult<IRouteableSystemNotification?>(null);
    }

    public void CloseAll() => Manager?.CloseAll();

    public Task InitializeAsync()
    {
        //if (Manager is { } manager)
        //    manager.();
        return Task.CompletedTask;
    }

    private EventHandler<RouteableSystemNotificationEventArgs>? _completed;

    public event EventHandler<RouteableSystemNotificationEventArgs>? NotificationCompleted
    {
        add
        {
            if (_completed is null && Manager is { } manager) manager.NotificationCompleted += OnCompleted;
            _completed += value;
        }
        remove
        {
            _completed -= value;
            if (_completed is null && Manager is { } manager) manager.NotificationCompleted -= OnCompleted;
        }
    }

    private void OnCompleted(object? sender, NativeNotificationCompletedEventArgs e)
        => _completed?.Invoke(this, new RouteableSystemNotificationEventArgs
        {
            ActionTag = e.ActionTag,
            NotificationId = e.NotificationId,
            UserData = e.UserData,
            IsCancelled = e.IsCancelled,
            IsActivated = e.IsActivated
        });
}

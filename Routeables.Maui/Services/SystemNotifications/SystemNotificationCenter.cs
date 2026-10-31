using System.Diagnostics;
using Routeables.Services;

namespace Routeables.Maui.Services;

/// <summary>
/// The system notifications (notification center of the platform) of <see cref="IRouteableNotificationService"/>: Android, iOS, Mac Catalyst and Windows
/// each have their own file (<c>SystemNotificationCenter.android.cs</c>...); the plain <c>net10.0</c> target has none and creates nothing.
/// One instance for the application: the platform calls it back (a tap, a dismissal) where no container is at hand.
/// </summary>
internal sealed partial class SystemNotificationCenter
{
    private readonly Dictionary<uint, SystemNotification> _active = [];
    private readonly object _gate = new();
    private int _lastId;
    private EventHandler<RouteableSystemNotificationEventArgs>? _completed;

    public static SystemNotificationCenter Current { get; } = new();

    /// <summary>The options given to <c>WithAppNotifications</c>; <see langword="null"/> until then: no system notification is created.</summary>
    internal AppNotificationOptions? Options { get; private set; }

    internal void Configure(AppNotificationOptions options)
    {
        Options = options;
        if (options.ClearOnAppClose)
            AppDomain.CurrentDomain.ProcessExit += (_, _) => CloseAll();
    }

    public event EventHandler<RouteableSystemNotificationEventArgs>? NotificationCompleted
    {
        add => _completed += value;
        remove => _completed -= value;
    }

    public IReadOnlyDictionary<uint, IRouteableSystemNotification> ActiveNotifications()
    {
        lock (_gate)
            return _active.ToDictionary(p => p.Key, p => (IRouteableSystemNotification)p.Value);
    }

    public async Task InitializeAsync()
    {
        if (Options is not null)
            await RequestPermissionAsync();
    }

    public async Task<IRouteableSystemNotification?> CreateNotificationAsync(string? category)
        => Options is not null && await RequestPermissionAsync() ? new SystemNotification(this, (uint)Interlocked.Increment(ref _lastId), category) : null;

    public void CloseAll()
    {
        uint[] ids;
        lock (_gate) ids = [.. _active.Keys];

        CancelAll();
        foreach (var id in ids)
            Complete(id, activated: false, action: null);
    }

    internal Task ShowAsync(SystemNotification notification)
    {
        lock (_gate) _active[notification.Id] = notification;
        try
        {
            Present(notification);
        }
        catch
        {
            lock (_gate) _active.Remove(notification.Id);
            throw;
        }

        // Not every platform lets a notification expire by itself: it is withdrawn from here.
        if (notification.Expiration is { } expiration && expiration > TimeSpan.Zero)
            _ = Task.Delay(expiration).ContinueWith(_ => CloseAsync(notification.Id), TaskScheduler.Default);

        return Task.CompletedTask;
    }

    internal Task CloseAsync(uint id)
    {
        bool known;
        lock (_gate) known = _active.ContainsKey(id);
        if (known)
        {
            Cancel(id);
            Complete(id, activated: false, action: null);
        }

        return Task.CompletedTask;
    }

    /// <summary>The platform tells what the user did: a tap (<paramref name="activated"/>) or a dismissal.</summary>
    internal void Complete(uint id, bool activated, string? action)
    {
        bool known;
        lock (_gate) known = _active.Remove(id);
        if (!known)
            return;

        try
        {
            _completed?.Invoke(this, new RouteableSystemNotificationEventArgs
            {
                NotificationId = id,
                ActionTag = action,
                IsActivated = activated,
                IsCancelled = !activated
            });
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Routeables: NotificationCompleted: {ex}");
        }
    }

    // What each platform does.
    private partial Task<bool> RequestPermissionAsync();

    private partial void Present(SystemNotification notification);

    private partial void Cancel(uint id);

    private partial void CancelAll();
}

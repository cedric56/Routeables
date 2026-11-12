using Routeables.Services;
using System.Diagnostics;

namespace Routeables.Native;

/// <summary>
/// The system notifications (notification center of the platform) of <see cref="IRouteableNotificationService"/>: Android, iOS, Mac Catalyst and Windows
/// each have their own file (<c>SystemNotificationCenter.android.cs</c>...); the plain <c>net10.0</c> target has none and creates nothing.
/// One instance for the application: the platform calls it back (a tap, a dismissal) where no container is at hand.
/// </summary>
public sealed partial class SystemNotificationCenter : IRouteableSystemNotificationSource
{
    private readonly Dictionary<uint, SystemNotification> _active = [];
    private readonly object _gate = new();
    private int _lastId;
    private EventHandler<RouteableSystemNotificationEventArgs>? _completed;

    /// <summary>The single instance used by the application.</summary>
    public static SystemNotificationCenter Current { get; } = new();

    /// <summary>Whether this build has a notification center: Android, iOS, Mac Catalyst and Windows do; the plain <c>net10.0</c> target has one on Linux
    /// (D-Bus) and on Windows (the notification area), none on macOS or in the browser.</summary>
    public static bool IsSupported =>
#if ANDROID || IOS || MACCATALYST || WINDOWS
        true;
#else
        OperatingSystem.IsLinux() || OperatingSystem.IsWindows() || OperatingSystem.IsBrowser();
#endif

    /// <summary>Whether this is a build for a platform of its own (Android, iOS, Mac Catalyst, Windows App SDK: an application of the platform, the web view of MAUI Blazor Hybrid
    /// included) and not the plain <c>net10.0</c> one, which also runs on a server.</summary>
    public static bool IsPlatformBuild =>
#if ANDROID || IOS || MACCATALYST || WINDOWS
        true;
#else
        false;
#endif

    /// <summary>The options given to <c>WithAppNotifications</c>; <see langword="null"/> until then: no system notification is created.</summary>
    public AppNotificationOptions? Options { get; private set; }

    /// <summary>Turns the notifications on with these options (nothing is created before).</summary>
    public void Configure(AppNotificationOptions options)
    {
        Options = options;
#if ANDROID
        SystemNotificationCenter.StartTracking();
#endif
        if (options.ClearOnAppClose)
            AppDomain.CurrentDomain.ProcessExit += (_, _) => CloseAll();
    }

    /// <inheritdoc />
    public event EventHandler<RouteableSystemNotificationEventArgs>? NotificationCompleted
    {
        add => _completed += value;
        remove => _completed -= value;
    }

    /// <summary>The notifications currently shown in the system notification center.</summary>
    public IReadOnlyDictionary<uint, IRouteableSystemNotification> ActiveNotifications()
    {
        lock (_gate)
            return _active.ToDictionary(p => p.Key, p => (IRouteableSystemNotification)p.Value);
    }

    /// <summary>Requests permission if options have been set; otherwise does nothing.</summary>
    public async Task InitializeAsync()
    {
        if (Options is { RequestPermissionOnInitialize: true })
            await RequestPermissionAsync();
    }

    /// <summary>
    /// Creates a new system notification when options are set and permission is granted; otherwise returns <see langword="null"/>.
    /// </summary>
    public async Task<IRouteableSystemNotification?> CreateNotificationAsync(string? category)
        => Options is not null && await RequestPermissionAsync() ? new SystemNotification(this, (uint)Interlocked.Increment(ref _lastId), category) : null;

    /// <summary>Cancels every active notification and raises <see cref="NotificationCompleted"/> for each one.</summary>
    public void CloseAll()
    {
        uint[] ids;
        lock (_gate) ids = [.. _active.Keys];

        CancelAll();
        foreach (var id in ids)
            Complete(id, activated: false, action: null);
    }

    // Tests replace the platform (a toast, a D-Bus call, a browser) so that the bookkeeping is tested on any operating system without showing anything.
    internal Action<SystemNotification>? PresentOverride { get; set; }

    internal Action<uint>? CancelOverride { get; set; }

    /// <summary>Shows the notification in the system center and schedules its expiration when one is set.</summary>
    internal Task ShowAsync(SystemNotification notification)
    {
        lock (_gate) _active[notification.Id] = notification;
        try
        {
            if (PresentOverride is { } present)
                present(notification);
            else
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

    /// <summary>Withdraws the notification with the given id from the system center, if it is still active.</summary>
    internal Task CloseAsync(uint id)
    {
        bool known;
        lock (_gate) known = _active.ContainsKey(id);
        if (known)
        {
            if (CancelOverride is { } cancel)
                cancel(id);
            else
                Cancel(id);
            Complete(id, activated: false, action: null);
        }

        return Task.CompletedTask;
    }

    /// <summary>The platform tells what the user did: a tap (<paramref name="activated"/>) or a dismissal.</summary>
    internal void Complete(uint id, bool activated, string? action, string? userData = null)
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
                UserData = userData,
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

    /// <summary>Asks the platform for permission to show notifications.</summary>
    private partial Task<bool> RequestPermissionAsync();

    /// <summary>Presents the notification in the system notification center.</summary>
    private partial void Present(SystemNotification notification);

    /// <summary>Cancels the notification with the given id on the platform.</summary>
    private partial void Cancel(uint id);

    /// <summary>Cancels every notification currently shown by the platform.</summary>
    private partial void CancelAll();
}
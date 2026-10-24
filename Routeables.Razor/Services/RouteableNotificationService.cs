using Routeables.Abstraction;
using Routeables.Services;

namespace Routeables.Razor.Services;

/// <summary>Where toast notifications appear and how many are visible at once.</summary>
internal sealed class RouteableNotificationOptions
{
    /// <summary>Gets or sets the screen corner or edge where notifications are stacked. Defaults to the top right.</summary>
    public RouteableNotificationPosition Position { get; set; } = RouteableNotificationPosition.TopRight;

    /// <summary>Gets or sets the maximum number of notifications displayed at once. The oldest one is closed beyond it.</summary>
    public int MaxDisplayed { get; set; } = 5;
}

/// <summary>A toast notification being displayed by <see cref="RouteableNotificationHost"/>.</summary>
internal sealed class ToastNotification(RouteableNotificationSession session)
{
    /// <summary>What the notification does, the same on every host.</summary>
    public RouteableNotificationSession Session { get; } = session;

    public string Title => Session.Title;

    public string Message => Session.Message;

    public RouteableNotificationType Type => Session.Type;

    public Timer? Expiration { get; set; }
}

/// <summary>
/// Implements <see cref="IRouteableNotificationService"/> without any UI library. Notifications are kept in a list
/// that a <see cref="RouteableNotificationHost"/> placed once in the application's layout displays.
/// Without a host, notifications are simply not visible.
/// </summary>
internal sealed class RouteableNotificationService(RouteableNotificationOptions options) : IRouteableNotificationService, IDisposable
{
    private readonly List<ToastNotification> items = [];
    private readonly object gate = new();

    /// <summary>Raised when the list of displayed notifications changes. The host subscribes to it to re-render.</summary>
    public event Action? Changed;

    /// <summary>Gets where notifications are stacked.</summary>
    public RouteableNotificationPosition Position => options.Position;

    /// <summary>Gets a snapshot of the displayed notifications, oldest first.</summary>
    public IReadOnlyList<ToastNotification> Items
    {
        get { lock (gate) return items.ToArray(); }
    }

    /// <inheritdoc />
    public void Show(
        string title,
        string message,
        RouteableNotificationType type = RouteableNotificationType.Information,
        TimeSpan? expiration = null,
        Action? onClick = null,
        Action? onClose = null)
    {
        var toast = new ToastNotification(new RouteableNotificationSession(title, message, type, expiration, onClick, onClose));

        List<ToastNotification> evicted = [];
        lock (gate)
        {
            items.Add(toast);
            while (items.Count > Math.Max(1, options.MaxDisplayed))
            {
                evicted.Add(items[0]);
                items.RemoveAt(0);
            }
        }

        // Five seconds without an expiration; RouteableNotificationDefaults.Never keeps it until the user closes it.
        if (toast.Session.AutoCloseAfter is { } delay)
            toast.Expiration = new Timer(_ => Close(toast), null, delay, Timeout.InfiniteTimeSpan);

        foreach (var old in evicted)
            Finish(old);

        Changed?.Invoke();
    }

    /// <summary>The user tapped <paramref name="toast"/>: its click callback runs, then it closes.</summary>
    public void Click(ToastNotification toast)
    {
        toast.Session.Click();
        Close(toast);
    }

    /// <summary>Closes <paramref name="toast"/>: it disappears and its close callback runs once.</summary>
    public void Close(ToastNotification toast)
    {
        bool removed;
        lock (gate) removed = items.Remove(toast);

        Finish(toast);
        if (removed)
            Changed?.Invoke();
    }

    private static void Finish(ToastNotification toast)
    {
        toast.Expiration?.Dispose();
        toast.Session.NotifyClosed();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (gate)
        {
            foreach (var toast in items)
                toast.Expiration?.Dispose();
            items.Clear();
        }
    }
}

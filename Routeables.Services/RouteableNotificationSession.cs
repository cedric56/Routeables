using Routeables.Abstraction;

namespace Routeables.Services;

/// <summary>
/// What a notification does, whatever the host draws it with: its texts, how long it stays, what a tap does and when its
/// close callback runs. A host draws it, calls <see cref="Click"/> when the user taps it and then closes it,
/// and calls <see cref="NotifyClosed"/> once it is gone, whatever the way it closed.
/// </summary>
public sealed class RouteableNotificationSession
{
    private readonly Action? _onClick;
    private readonly Action? _onClose;
    private int _closed;

    /// <summary>Starts the session of a notification.</summary>
    /// <param name="title">The title; empty when <see langword="null"/>.</param>
    /// <param name="message">The message; empty when <see langword="null"/>.</param>
    /// <param name="type">The type, which the host shows as it likes (colour, icon).</param>
    /// <param name="expiration">How long it stays; <see cref="RouteableNotificationDefaults.Expiration"/> when <see langword="null"/>.</param>
    /// <param name="onClick">Called when the user taps the notification.</param>
    /// <param name="onClose">Called once, when the notification is gone.</param>
    public RouteableNotificationSession(
        string? title,
        string? message,
        RouteableNotificationType type = RouteableNotificationType.Information,
        TimeSpan? expiration = null,
        Action? onClick = null,
        Action? onClose = null)
    {
        Title = title ?? string.Empty;
        Message = message ?? string.Empty;
        Type = type;
        _onClick = onClick;
        _onClose = onClose;

        var delay = expiration ?? RouteableNotificationDefaults.Expiration;
        AutoCloseAfter = delay == RouteableNotificationDefaults.Never || delay <= TimeSpan.Zero ? null : delay;
    }

    /// <summary>The title.</summary>
    public string Title { get; }

    /// <summary>The message.</summary>
    public string Message { get; }

    /// <summary>The type.</summary>
    public RouteableNotificationType Type { get; }

    /// <summary>
    /// How long the notification stays before it closes by itself; <see langword="null"/> when it stays until it is closed
    /// (<see cref="RouteableNotificationDefaults.Never"/>, or a delay that is not positive).
    /// </summary>
    public TimeSpan? AutoCloseAfter { get; }

    /// <summary>Whether <see cref="NotifyClosed"/> has been called.</summary>
    public bool IsClosed => Volatile.Read(ref _closed) == 1;

    /// <summary>The user tapped the notification: runs its click callback, if any. The host then closes the notification.</summary>
    public void Click() => RouteableErrors.Guard(null, "Notification.Click", _onClick);

    /// <summary>
    /// The notification is gone: runs its close callback, once. Returns <see langword="true"/> the first time and
    /// <see langword="false"/> after, so the host can call it from every way a notification closes.
    /// </summary>
    public bool NotifyClosed()
    {
        if (Interlocked.Exchange(ref _closed, 1) != 0)
            return false;

        RouteableErrors.Guard(null, "Notification.Close", _onClose);
        return true;
    }
}

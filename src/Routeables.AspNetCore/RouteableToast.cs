using Routeables.Services;

namespace Routeables.AspNetCore;

/// <summary>A notification the next page shows. The server cannot push it: it appears with the page the visitor gets next.</summary>
public sealed class RouteableToast(int id, string title, string message, RouteableNotificationType type, TimeSpan? expiration, Action? onClick, Action? onClose)
{
    /// <summary>Unique identifier of this toast within the current session.</summary>
    public int Id { get; } = id;

    /// <summary>The toast title.</summary>
    public string Title { get; } = title;

    /// <summary>The toast body text.</summary>
    public string Message { get; } = message;

    /// <summary>Severity / visual style of the notification.</summary>
    public RouteableNotificationType Type { get; } = type;

    /// <summary>How long the toast should remain visible; <see langword="null"/> means it stays until dismissed.</summary>
    public TimeSpan? Expiration { get; } = expiration;

    /// <summary>UTC time when the toast was created (used to decide whether it has expired).</summary>
    public DateTime Shown { get; } = DateTime.UtcNow;

    /// <summary>Optional action raised when the visitor clicks the toast.</summary>
    internal Action? OnClick { get; } = onClick;

    /// <summary>Optional action raised when the toast is closed or expires.</summary>
    internal Action? OnClose { get; } = onClose;
}
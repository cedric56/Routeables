using Routeables.Services;

namespace Routeables.AspNetCore;

/// <summary>A notification the next page shows. The server cannot push it: it appears with the page the visitor gets next.</summary>
public sealed class RouteableToast(int id, string title, string message, RouteableNotificationType type, TimeSpan? expiration, Action? onClick, Action? onClose)
{
    public int Id { get; } = id;

    public string Title { get; } = title;

    public string Message { get; } = message;

    public RouteableNotificationType Type { get; } = type;

    public TimeSpan? Expiration { get; } = expiration;

    public DateTime Shown { get; } = DateTime.UtcNow;

    internal Action? OnClick { get; } = onClick;

    internal Action? OnClose { get; } = onClose;
}

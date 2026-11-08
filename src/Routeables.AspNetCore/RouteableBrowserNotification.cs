using Routeables.Services;

namespace Routeables.AspNetCore;

/// <summary>
/// A system notification of the web host: the notification center of the visitor's browser (its Notification API). The server only
/// asks: <see cref="ShowAsync"/> queues the notification, and the page that is rendered next hands it to the browser (see
/// <see cref="RouteableInteractions.TakeSystemNotificationsJson"/>). The browser decides: the visitor must have allowed notifications, and the
/// server is not told what he did with it (<see cref="IRouteableNotificationService.NotificationCompleted"/> is never raised).
/// </summary>
public sealed class RouteableBrowserNotification : IRouteableSystemNotification
{
    private readonly RouteableInteractions _interactions;
    private IReadOnlyList<RouteableSystemNotificationAction>? _actions;

    internal RouteableBrowserNotification(RouteableInteractions interactions, uint id, string? category)
    {
        _interactions = interactions;
        Id = id;
        Category = category;
    }

    /// <inheritdoc />
    public uint Id { get; }

    /// <inheritdoc />
    public string? Category { get; }

    /// <inheritdoc />
    public string? Title { get; set; }

    /// <inheritdoc />
    public string? Tag { get; set; }

    /// <inheritdoc />
    public string? Message { get; set; }

    /// <inheritdoc />
    public TimeSpan? Expiration { get; set; }

    /// <inheritdoc />
    public string? Icon { get; set; }

    /// <inheritdoc />
    public string? ReplyActionTag { get; set; }

    /// <inheritdoc />
    public IReadOnlyList<RouteableSystemNotificationAction>? Actions => _actions;

    /// <inheritdoc />
    public void SetActions(IReadOnlyList<RouteableSystemNotificationAction>? actions) => _actions = actions is { Count: > 0 } ? actions : null;

    /// <inheritdoc />
    public Task ShowAsync()
    {
        _interactions.ShowSystemNotification(this);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task CloseAsync()
    {
        _interactions.CloseSystemNotification(Id);
        return Task.CompletedTask;
    }
}

/// <summary>What the page tells the browser to do with a system notification: <c>show</c>, <c>close</c> or <c>closeAll</c>.</summary>
/// <param name="Op"><c>show</c>, <c>close</c> or <c>closeAll</c>.</param>
/// <param name="Id">The <see cref="IRouteableSystemNotification.Id"/>; 0 for <c>closeAll</c>.</param>
/// <param name="Title">The title, for <c>show</c>.</param>
/// <param name="Message">The body text, for <c>show</c>.</param>
/// <param name="Icon">The address of the icon, for <c>show</c>.</param>
/// <param name="Tag">The tag: a notification with the tag of a shown one replaces it.</param>
/// <param name="ExpirationMs">How long it stays, in milliseconds; <see langword="null"/> until the visitor closes it.</param>
public sealed record RouteableBrowserNotificationCommand(string Op, uint Id, string? Title = null, string? Message = null, string? Icon = null, string? Tag = null, int? ExpirationMs = null);

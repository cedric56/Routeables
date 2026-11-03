using Routeables.Services;

namespace Routeables.Maui.Services;

/// <summary>A system notification of the platform, not shown until <see cref="ShowAsync"/>. The platforms draw the title and the message;
/// the actions are kept but not drawn.</summary>
internal sealed class SystemNotification(SystemNotificationCenter center, uint id, string? category) : IRouteableSystemNotification
{
    public uint Id { get; } = id;
    public string? Category { get; } = category;
    public string? Title { get; set; }
    public string? Tag { get; set; }
    public string? Message { get; set; }
    public TimeSpan? Expiration { get; set; }
    public string? Icon { get; set; }
    public string? ReplyActionTag { get; set; }
    public IReadOnlyList<RouteableSystemNotificationAction>? Actions { get; private set; }

    public void SetActions(IReadOnlyList<RouteableSystemNotificationAction>? actions) => Actions = actions;

    public Task ShowAsync() => center.ShowAsync(this);

    public Task CloseAsync() => center.CloseAsync(Id);
}

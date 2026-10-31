using Avalonia.Labs.Notifications;
using Routeables.Services;
using Bitmap = Avalonia.Media.Imaging.Bitmap;

namespace Routeables.Avalonia.Services;

/// <summary>An <see cref="INativeNotification"/> of Avalonia.Labs.Notifications seen as an <see cref="IRouteableSystemNotification"/>.
/// This file and <c>RouteableNotificationService.System.cs</c> are the only ones that know the package.</summary>
internal sealed class RouteableSystemNotification(INativeNotification inner) : IRouteableSystemNotification
{
    public uint Id => inner.Id;

    public string? Category => inner.Category;

    public string? Title { get => inner.Title; set => inner.Title = value; }

    public string? Tag { get => inner.Tag; set => inner.Tag = value; }

    public string? Message { get => inner.Message; set => inner.Message = value; }

    public TimeSpan? Expiration { get => inner.Expiration; set => inner.Expiration = value; }

    public string? Icon { get => inner.Icon?.ToString(); set => inner.Icon = value is { } url ? new Bitmap(url) : null; }

    public string? ReplyActionTag { get => inner.ReplyActionTag; set => inner.ReplyActionTag = value; }

    public IReadOnlyList<RouteableSystemNotificationAction>? Actions
        => inner.Actions?.Select(a => new RouteableSystemNotificationAction(a.Caption, a.Tag) { Icon = a.Icon?.ToString() }).ToList();

    public void SetActions(IReadOnlyList<RouteableSystemNotificationAction>? actions)
        => inner.SetActions(actions?.Select(a => new NativeNotificationAction(a.caption, a.tag) { Icon = a.Icon is { } url ? new Bitmap(url) : null }).ToList() ?? []);

    public Task ShowAsync()
    {
        inner.Show();
        return Task.CompletedTask;
    }

    public Task CloseAsync()
    {
        inner.Close();
        return Task.CompletedTask;
    }
}

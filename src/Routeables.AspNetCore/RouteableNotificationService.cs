using Routeables.Services;

namespace Routeables.AspNetCore;

/// <summary>The notifications of the web host: the page the visitor gets next shows them. There is no notification center of the system to offer.</summary>
public sealed class RouteableNotificationService(RouteableInteractions interactions) : IRouteableNotificationService
{
    public void Show(string title, string message, RouteableNotificationType type = RouteableNotificationType.Information, TimeSpan? expiration = null, Action? onClick = null, Action? onClose = null)
        => interactions.Notify(title, message, type, expiration, onClick, onClose);
}

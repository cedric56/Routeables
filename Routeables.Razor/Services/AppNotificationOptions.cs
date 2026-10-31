namespace Routeables.Razor.Services;

/// <summary>
/// Options of the system notifications of the browser, given to <see cref="Extensions.WithAppNotifications"/> like the <c>AppNotificationOptions</c>
/// of Avalonia.Labs.Notifications. Its <c>ClearOnAppClose</c> is here too; the application id and the channels do not apply to a browser.
/// </summary>
public sealed class AppNotificationOptions
{
    /// <summary>
    /// The address of the service worker (<c>routeables-notifications-worker.js</c> of this package, copied to the root of the site): it gives a notification
    /// action buttons and a reply field. When it cannot be registered (not served, not a secure page), the notifications are made by the page itself, with
    /// no buttons. <see langword="null"/> never uses a service worker.
    /// </summary>
    public string? ServiceWorkerUrl { get; set; } = "/routeables-notifications-worker.js";

    /// <summary>The icon of a notification that has none (the address of an image); <see langword="null"/> for the one of the browser.</summary>
    public string? DefaultIcon { get; set; }

    /// <summary>Whether the notifications still displayed are closed when the application ends (the circuit, the tab). <see langword="false"/> by default.</summary>
    public bool ClearOnAppClose { get; set; }

    /// <summary>
    /// Whether <see cref="Routeables.Services.IRouteableNotificationService.InitializeAsync"/> asks the user for the permission to notify.
    /// A browser only accepts the question after a gesture of the user (a click): call it from a click handler. <see langword="true"/> by default.
    /// </summary>
    public bool RequestPermissionOnInitialize { get; set; } = true;
}

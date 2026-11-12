namespace Routeables.Native;

/// <summary>
/// Options of the system notifications, given to <c>WithAppNotifications</c>.
/// </summary>
public sealed class AppNotificationOptions
{
    /// <summary>Whether the notifications still displayed are withdrawn when the application ends. <see langword="false"/> by default.</summary>
    public bool ClearOnAppClose { get; set; }

    /// <summary>The name of the notification channel the user sees in the settings of Android 8 and later.</summary>
    public string ChannelName { get; set; } = "Notifications";

    /// <summary>
    /// Browser (WebAssembly) only: the address of the service worker (<c>routeables-notifications-worker.js</c>, served by <c>Routeables.Blazor</c> or copied to the
    /// root of the site, next to its <c>&lt;base href&gt;</c>) that gives a notification action buttons and a reply field. When it cannot be registered, the page notifies
    /// by itself, with no buttons. <see langword="null"/> never uses a service worker.
    /// </summary>
    public string? ServiceWorkerUrl { get; set; } = "routeables-notifications-worker.js";

    /// <summary>Browser only: the icon of a notification that has none (the address of an image).</summary>
    public string? DefaultIcon { get; set; }

    /// <summary>
    /// Whether <c>InitializeAsync</c> asks the user for the permission to notify. A browser only accepts the question after a gesture of the user (a click): call it from a
    /// click handler. <see langword="true"/> by default.
    /// </summary>
    public bool RequestPermissionOnInitialize { get; set; } = true;
}

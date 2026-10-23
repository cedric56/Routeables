using System.ComponentModel;
namespace Routeables.Services;

/// <summary>
/// A single action button attached to a system notification (e.g. "Reply",
/// "Dismiss", "Snooze").
/// </summary>
/// <param name="caption">The user-visible label for the action button.</param>
/// <param name="tag">
/// An opaque identifier for this action, echoed back in
/// <see cref="RouteableSystemNotificationEventArgs.ActionTag"/> when the user taps it,
/// so the app can tell which action was invoked.
/// </param>
[EditorBrowsable(EditorBrowsableState.Never)]
public record RouteableSystemNotificationAction(string caption, string tag)
{
    /// <summary>
    /// Gets the icon to show on the action button, if the platform supports
    /// per-action icons. <see langword="null"/> for no icon.
    /// </summary>
    public string? Icon { get; init; }
}

/// <summary>
/// Event data raised when a user interacts with (or dismisses) a system
/// notification — see <see cref="IRouteableSystemNotificationService.NotificationCompleted"/>.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public class RouteableSystemNotificationEventArgs : EventArgs
{
    /// <summary>
    /// Gets the <see cref="RouteableSystemNotificationAction.caption"/>-matching tag
    /// of the action the user tapped, or <see langword="null"/> if the
    /// notification body itself was tapped (no specific action) or if it
    /// was dismissed without tapping anything.
    /// </summary>
    public string? ActionTag { get; init; }

    /// <summary>
    /// Gets the <see cref="IRouteableSystemNotification.Id"/> of the notification
    /// this event relates to.
    /// </summary>
    public uint? NotificationId { get; init; }

    /// <summary>
    /// Gets platform-specific extra data associated with this event.
    /// Could be used for text input — e.g. the text the user typed into a
    /// reply action (see <see cref="IRouteableSystemNotification.ReplyActionTag"/>).
    /// </summary>
    public object? UserData { get; init; }

    /// <summary>
    /// Gets whether the notification was dismissed/cancelled by the user
    /// without an action being invoked.
    /// </summary>
    public bool IsCancelled { get; init; }

    /// <summary>
    /// Gets whether the notification (or one of its actions) was actively
    /// tapped/activated by the user, as opposed to expiring or being closed
    /// programmatically.
    /// </summary>
    public bool IsActivated { get; init; }
}

/// <summary>
/// A single system-level (OS notification-center) notification, created via
/// <see cref="IRouteableSystemNotificationService.CreateNotificationAsync"/>. Mutate the
/// properties then call <see cref="ShowAsync"/> to display or update it.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IRouteableSystemNotification
{
    /// <summary>
    /// Gets the unique identifier for this notification instance, as tracked
    /// by <see cref="IRouteableSystemNotificationService.ActiveNotifications"/>.
    /// </summary>
    uint Id { get; }

    /// <summary>
    /// Gets the category this notification belongs to. Categories are
    /// defined at app launch and determine which actions are available by
    /// default. On Android, a category also acts as the notification channel.
    /// </summary>
    string? Category { get; }

    /// <summary>
    /// Gets or sets the notification's title.
    /// </summary>
    string? Title { get; set; }

    /// <summary>
    /// Gets or sets a tag identifying this notification, e.g. so a later
    /// call can find/update/replace it.
    /// </summary>
    string? Tag { get; set; }

    /// <summary>
    /// Gets or sets the notification's body text.
    /// </summary>
    string? Message { get; set; }

    /// <summary>
    /// Gets or sets how long the notification remains visible before
    /// auto-expiring. Not supported on web — setting this has no effect
    /// there.
    /// </summary>
    TimeSpan? Expiration { get; set; }

    /// <summary>
    /// Gets or sets the icon shown on the notification. Only supported on
    /// web — setting this has no effect on other platforms.
    /// </summary>
    string? Icon { get; set; }

    /// <summary>
    /// Gets or sets the action tag that, when set, enables text input on
    /// the notification and designates the matching action as the "reply"
    /// action (its typed text arrives via
    /// <see cref="RouteableSystemNotificationEventArgs.UserData"/>).
    /// </summary>
    string? ReplyActionTag { get; set; }

    /// <summary>
    /// Gets the action buttons currently attached to this notification.
    /// The available set is constrained by the notification's
    /// <see cref="Category"/> (categories are defined at launch).
    /// </summary>
    IReadOnlyList<RouteableSystemNotificationAction>? Actions { get; }

    /// <summary>
    /// Sets the action buttons attached to this notification. No-op on iOS.
    /// </summary>
    /// <param name="actions">The actions to attach, or <see langword="null"/>/empty for none.</param>
    void SetActions(IReadOnlyList<RouteableSystemNotificationAction>? actions);

    /// <summary>
    /// Displays the notification. Can be called multiple times on the same
    /// instance to update an already-active notification in place (e.g.
    /// after changing <see cref="Title"/>/<see cref="Message"/>).
    /// </summary>
    Task ShowAsync();

    /// <summary>
    /// Closes/dismisses the notification if it's currently shown.
    /// </summary>
    Task CloseAsync();
}

/// <summary>
/// Creates and tracks system-level (OS notification-center) notifications,
/// in a platform-agnostic way, so ViewModels can raise OS notifications
/// without knowing whether the app is running on Avalonia, MAUI, or Blazor.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IRouteableSystemNotificationService
{
    /// <summary>
    /// Gets the notifications currently active (shown and not yet closed),
    /// keyed by <see cref="IRouteableSystemNotification.Id"/>.
    /// </summary>
    IReadOnlyDictionary<uint, IRouteableSystemNotification> ActiveNotifications();

    /// <summary>
    /// Creates a new, not-yet-shown notification. Call
    /// <see cref="IRouteableSystemNotification.ShowAsync"/> on the result to display it.
    /// </summary>
    /// <param name="category">
    /// The category to create the notification under. If
    /// <see langword="null"/>, the implementation uses a default category;
    /// otherwise the category must already be defined at app launch.
    /// </param>
    /// <returns>
    /// The new notification, or <see langword="null"/> if it couldn't be
    /// created (e.g. an unrecognized <paramref name="category"/>, or the
    /// platform denies notification permission).
    /// </returns>
    Task<IRouteableSystemNotification?> CreateNotificationAsync(string? category);

    /// <summary>
    /// Closes every currently active notification.
    /// </summary>
    void CloseAll();

    /// <summary>
    /// Raised when the user interacts with or dismisses a notification —
    /// see <see cref="RouteableSystemNotificationEventArgs"/> for what triggered it.
    /// </summary>
    event EventHandler<RouteableSystemNotificationEventArgs>? NotificationCompleted;

    /// <summary>
    /// Performs any platform-specific setup needed before notifications can
    /// be created (e.g. requesting OS permission, registering categories).
    /// Default implementation is a no-op; platform adapters that need setup
    /// should override this.
    /// </summary>
    Task InitializeAsync()
    {
        return Task.CompletedTask;
    }
}
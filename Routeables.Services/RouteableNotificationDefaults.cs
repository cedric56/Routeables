namespace Routeables.Services;

/// <summary>What every host does with the <c>expiration</c> of <see cref="IRouteableNotificationService.Show"/>.</summary>
public static class RouteableNotificationDefaults
{
    /// <summary>How long a notification stays when no expiration is given: five seconds, on every host.</summary>
    public static readonly TimeSpan Expiration = TimeSpan.FromSeconds(5);

    /// <summary>The expiration that keeps a notification on screen until the user closes it or taps it.</summary>
    public static readonly TimeSpan Never = Timeout.InfiniteTimeSpan;
}

using Routeables.Abstraction;
using Routeables.Routing;

namespace Routeables.Segments;

/// <summary>Raised before a <see cref="RouteCoordination"/> touches any router.</summary>
public sealed class RouteCoordinatingEventArgs(RouteCoordination coordination) : EventArgs
{
    /// <summary>The coordination about to run.</summary>
    public RouteCoordination Coordination { get; } = coordination;
}

/// <summary>Raised exactly once after every <see cref="IRouteCoordinator.Coordinating"/>, whatever the outcome.</summary>
public sealed class RouteCoordinatedEventArgs(RouteCoordination coordination, RouteableResult result, int completed) : EventArgs
{
    /// <summary>The coordination that ran.</summary>
    public RouteCoordination Coordination { get; } = coordination;

    /// <summary>The outcome of the last segment that ran, or the reason the coordination could not start.</summary>
    public RouteableResult Result { get; } = result;

    /// <summary>How many segments completed successfully (earlier ones are not rolled back).</summary>
    public int CompletedSegments { get; } = completed;
}

/// <summary>
/// Sits above <see cref="IRouter"/>: turns a <see cref="RouteCoordination"/> into one coordination per segment and
/// hands each to the router of its region, in order.
/// </summary>
public interface IRouteCoordinator
{
    /// <summary>Raised before any router is touched. Not raised when the coordination is rejected up front.</summary>
    event EventHandler<RouteCoordinatingEventArgs>? Coordinating;

    /// <summary>Raised once the coordination ended (succeeded, was cancelled by a guard, or failed).</summary>
    event EventHandler<RouteCoordinatedEventArgs>? Coordinated;

    /// <summary>
    /// Runs the segments in order and stops at the first one that is cancelled or fails; segments already
    /// completed stay in their router's history. Unknown segment names and regions are detected before any router
    /// is touched.
    /// </summary>
    Task<RouteableResult> CoordinateAsync(RouteCoordination coordination, CancellationToken cancellationToken = default);
}

/// <summary>Convenience overloads for <see cref="IRouteCoordinator"/>.</summary>
public static class RouteCoordinatorExtensions
{
    /// <summary>Parses <paramref name="uri"/> (see <see cref="RouteCoordination"/>) and goes to it.</summary>
    /// <returns>A failed result when the URI is malformed; otherwise the coordination's result.</returns>
    public static Task<RouteableResult> CoordinateAsync(
        this IRouteCoordinator coordinator, string uri, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(coordinator);
        return RouteCoordination.TryParse(uri, out var coordination, out var error)
            ? coordinator.CoordinateAsync(coordination, cancellationToken)
            : Task.FromResult(RouteableResult.Fail(error));
    }
}
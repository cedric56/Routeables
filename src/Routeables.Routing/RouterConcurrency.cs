namespace Routeables.Routing;

/// <summary>What a router does with a route change that comes while another one is in progress.</summary>
public enum RouterConcurrency
{
    /// <summary>The default: the new route change waits for the one in progress, and they run one after the other, in order.</summary>
    Queue = 0,

    /// <summary>
    /// The new route change cancels the one in progress through the <see cref="CancellationToken"/> that its guards and lifecycle methods
    /// receive (the route change ends as cancelled), then takes its place. When several come in a row, only the last one runs.
    /// </summary>
    CancelCurrent = 1,
}

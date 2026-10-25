namespace Routeables.Abstraction;

public sealed class RouteableRoutedEventArgs(
RouteableKind kind, IRouterEntry? from, IRouterEntry to,
RouteableOutcome outcome, Exception? exception = null)
: RouteableRoutingEventArgs(kind, from, to)
{
    public RouteableOutcome Outcome { get; } = outcome;
    public bool Succeeded => Outcome == RouteableOutcome.Success;
    /// <summary>Set only when <see cref="Outcome"/> is Failed.</summary>
    public Exception? Exception { get; } = exception;
}

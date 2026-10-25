namespace Routeables.Abstraction;

/// <summary>Describes a route change that ended: what kind it was, the entries involved and how it ended.</summary>
/// <param name="kind">The kind of route change.</param>
/// <param name="from">The entry that was left; <see langword="null"/> on the first route change.</param>
/// <param name="to">The entry that was entered.</param>
/// <param name="outcome">How the route change ended.</param>
/// <param name="exception">The error, when <paramref name="outcome"/> is <see cref="RouteableOutcome.Failed"/>.</param>
public sealed class RouteableRoutedEventArgs(
RouteableKind kind, IHistoryEntry? from, IHistoryEntry to,
RouteableOutcome outcome, Exception? exception = null)
: RouteableRoutingEventArgs(kind, from, to)
{
    /// <summary>How the route change ended.</summary>
    public RouteableOutcome Outcome { get; } = outcome;
    /// <summary>Whether the route change succeeded.</summary>
    public bool Succeeded => Outcome == RouteableOutcome.Success;
    /// <summary>Set only when <see cref="Outcome"/> is Failed.</summary>
    public Exception? Exception { get; } = exception;
}

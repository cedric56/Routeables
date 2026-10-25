namespace Routeables.Abstraction;

/// <summary>Describes a route change that is under way: what kind it is, the entry it leaves and the entry it goes to.</summary>
/// <param name="kind">The kind of route change.</param>
/// <param name="from">The entry being left; <see langword="null"/> on the first route change.</param>
/// <param name="to">The entry being entered.</param>
public class RouteableRoutingEventArgs(RouteableKind kind, IHistoryEntry? from, IHistoryEntry to) : EventArgs
{
    /// <summary>The kind of route change.</summary>
    public RouteableKind Kind { get; } = kind;
    /// <summary>The entry being left; null on the first route change.</summary>
    public IHistoryEntry? From { get; } = from;
    /// <summary>The entry being entered.</summary>
    public IHistoryEntry To { get; } = to;
}

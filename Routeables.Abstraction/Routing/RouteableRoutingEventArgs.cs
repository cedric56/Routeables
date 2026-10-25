namespace Routeables.Abstraction;

public class RouteableRoutingEventArgs(RouteableKind kind, IRouterEntry? from, IRouterEntry to) : EventArgs
{
    public RouteableKind Kind { get; } = kind;
    /// <summary>The entry being left; null on the first route change.</summary>
    public IRouterEntry? From { get; } = from;
    /// <summary>The entry being entered.</summary>
    public IRouterEntry To { get; } = to;
}

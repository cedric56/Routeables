namespace Routeables.Abstraction;

/// <summary>
/// A view that is bound to its view model by the router: it gets the view model as its <see cref="Context"/> when it is shown.
/// The base types of each host (<c>RouteableFor</c>) implement it.
/// </summary>
public interface IRouteableFor
{
    /// <summary>The view model of this view, set by the router before the view is shown (a data context, a binding context).</summary>
    object? Context { get; set; }
}

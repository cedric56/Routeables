using Routeables.Abstraction;

namespace Routeables.Routing;

/// <summary>
/// Represents an individual entry in the router history, pairing a view model, a view, and routing arguments.
/// </summary>
/// <param name="ViewModel">The view model instance associated with this history entry.</param>
/// <param name="View">The view instance associated with this history entry.</param>
/// <param name="Args">The route change arguments passed to this entry.</param>
internal sealed record RouterEntry(
    object ViewModel,
    object View,
    IRouteableArgs Args) : IHistoryEntry
{
    /// <inheritdoc />
    IRouteableArgs IHistoryEntry.Args => Args;
}

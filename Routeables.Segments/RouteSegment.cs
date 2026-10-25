using Routeables.Abstraction;
using Routeables.Routing;

namespace Routeables.Segments;

/// <summary>What a <see cref="RouteSegment"/> asks its router to do.</summary>
public enum RouteSegmentKind
{
    /// <summary>Route to the registered segment, pushing a new history entry.</summary>
    Push,

    /// <summary>Go back one entry and discard the one that was left (<c>..</c>).</summary>
    Back,
}

/// <summary>
/// One step of a <see cref="RouteCoordination"/>: a registered segment <paramref name="Name"/>, the router
/// (region) that handles it and the parameters that belong to this segment only.
/// </summary>
/// <param name="Name">The registered segment name, or <c>..</c> for <see cref="RouteSegmentKind.Back"/>.</param>
/// <param name="Region">The region whose router handles the segment; <see langword="null"/> for the default router.</param>
/// <param name="Kind">Whether the segment pushes a view or goes back.</param>
/// <param name="Parameters">
/// The segment's own named parameters (the query of a URI). A key may repeat, in which case the destination receives
/// every value as an array. A <see langword="null"/> value is a flag without a value (<c>?flag</c>). They are delivered
/// to the view model by name (see <see cref="IRouteableArgs.ForViewModel"/>) and kept in the neutral values.
/// </param>
/// <param name="Configure">
/// Fills the <see cref="IRouteableArgs"/> of this segment directly, with everything <c>RouteableArgs</c> offers:
/// <see cref="IRouteableArgs.ForViewModel"/> (positional or named entries), <see cref="IRouteableArgs.ForView"/> and
/// <see cref="IRouteableArgs.ForValues"/>. It runs after <paramref name="Parameters"/> were applied; as with <c>RouteableArgs</c>, the same named entry must not be given twice. It cannot be written in a
/// URI, so <see cref="RouteCoordination.ToString"/> leaves it out.
/// </param>
public sealed record RouteSegment(
    string Name,
    string? Region = null,
    RouteSegmentKind Kind = RouteSegmentKind.Push,
    IReadOnlyList<KeyValuePair<string, object?>>? Parameters = null,
    Action<IRouteableArgs>? Configure = null)
{
    /// <summary>The name used by <see cref="RouteSegmentKind.Back"/> segments.</summary>
    public const string BackName = "..";
}
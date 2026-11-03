using Routeables.Abstraction;

namespace Routeables.Regions;

/// <summary>What a region host knows about the route change that put a view on screen: which way it moved, and the transition asked for.</summary>
/// <param name="Direction">Which way the history moved.</param>
/// <param name="Transition">The transition given with the navigation (<see cref="IRouteableArgs.Transition"/>); <see langword="null"/> when none was.</param>
public sealed record RegionChange(NavigationDirection Direction, RouteTransition? Transition)
{
    /// <summary>A change nobody asked a transition for (the history was emptied, or the first view).</summary>
    public static RegionChange Unknown { get; } = new(NavigationDirection.None, null);

    /// <summary>
    /// The transition to play: the one of the navigation, else the one of the region, else (to keep the old
    /// <c>TransitionDuration</c> working) a fade when the region has a length, else none.
    /// </summary>
    /// <param name="regionTransition">The transition the region declares, or <see langword="null"/>.</param>
    /// <param name="regionDuration">The length the region declares; zero for none.</param>
    public RegionTransition Resolve(RouteTransition? regionTransition, TimeSpan regionDuration)
    {
        var chosen = Transition ?? regionTransition;
        var declared = chosen is not null;
        chosen ??= regionDuration > TimeSpan.Zero ? RouteTransition.Fade : RouteTransition.None;
        var duration = chosen.Duration ?? (regionDuration > TimeSpan.Zero ? regionDuration : RouteTransition.DefaultDuration);
        return new RegionTransition(chosen, duration, Direction, declared);
    }
}

/// <summary>The transition a host plays for one change.</summary>
/// <param name="Transition">What to play.</param>
/// <param name="Duration">How long it takes.</param>
/// <param name="Direction">Which way the history moved.</param>
/// <param name="Declared">Whether a navigation or the region asked for it (otherwise it only comes from the old <c>TransitionDuration</c> or from nothing).</param>
public readonly record struct RegionTransition(RouteTransition Transition, TimeSpan Duration, NavigationDirection Direction, bool Declared)
{
    /// <summary>Whether the view appears at once.</summary>
    public bool IsNone => Transition.IsNone || Duration <= TimeSpan.Zero;
}

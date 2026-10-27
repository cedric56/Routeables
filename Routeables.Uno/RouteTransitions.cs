using System.Collections.Concurrent;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Routeables.Abstraction;
using Routeables.Regions;

namespace Routeables.Uno;

/// <summary>
/// The transitions a <see cref="RouteableRegion"/> plays for a name (<see cref="RouteTransition"/>): the built-in ones
/// (<c>Fade</c>, <c>Slide</c>, <c>SlideUp</c>) and the custom ones registered here. They are the theme transitions of the platform,
/// which chooses how long they last.
/// </summary>
public static class RouteTransitions
{
    private static readonly ConcurrentDictionary<string, Func<NavigationDirection, TransitionCollection?>> Custom =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registers a transition under a name, then <c>args.WithTransition("Pop")</c> or <c>&lt;routeables:RouteableRegion Transition="Pop" /&gt;</c> plays it.
    /// A name that is a built-in one replaces it.
    /// </summary>
    /// <param name="name">The name of the transition.</param>
    /// <param name="create">Builds the transitions the region plays when its content changes: the way the history moved.</param>
    public static void Register(string name, Func<NavigationDirection, TransitionCollection?> create)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(create);
        Custom[name.Trim()] = create;
    }

    internal static TransitionCollection? Create(RegionTransition transition, double width, double height)
    {
        var name = transition.Transition.Name;
        if (Custom.TryGetValue(name, out var create))
            return create(transition.Direction);

        if (transition.IsNone)
            return null;

        var sign = transition.Direction == NavigationDirection.Back ? -1 : 1;
        if (name.Equals("Fade", StringComparison.OrdinalIgnoreCase))
            return new TransitionCollection { new ContentThemeTransition() };
        if (name.Equals("Slide", StringComparison.OrdinalIgnoreCase))
            return new TransitionCollection { new EntranceThemeTransition { FromHorizontalOffset = sign * Math.Max(40, width * 0.2) } };
        if (name.Equals("SlideUp", StringComparison.OrdinalIgnoreCase))
            return new TransitionCollection { new EntranceThemeTransition { FromVerticalOffset = sign * Math.Max(40, height * 0.2) } };

        // An unknown name must not stop the view from being shown.
        System.Diagnostics.Debug.WriteLine($"Routeables: no transition named '{name}' (register it with RouteTransitions.Register); the view appears at once.");
        return null;
    }
}

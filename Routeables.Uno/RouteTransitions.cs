using System.Collections.Concurrent;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Routeables.Abstraction;
using Routeables.Regions;

namespace Routeables.Uno;

/// <summary>
/// The transitions a <see cref="RouteableRegion"/> plays for a name (<see cref="RouteTransition"/>): the built-in ones
/// (<see cref="RouteTransition.BuiltIn"/>) and the custom ones registered here. They are the theme transitions of the platform, which
/// chooses how long they last, so the built-in ones are the closest of them: <c>Fade</c>, <c>Blur</c> and <c>Spotlight</c> are a
/// <see cref="ContentThemeTransition"/>; <c>Slide</c>, <c>SlideFade</c> and <c>Swipe</c> an <see cref="EntranceThemeTransition"/> from the side;
/// <c>SlideUp</c> and <c>Lift</c> one from below; <c>Pop</c>, <c>Scale</c>, <c>Flip</c>, <c>Rotate</c>, <c>Reveal</c> and <c>Curtain</c> a short one from below
/// (Uno does not implement <c>PopupThemeTransition</c>). Register a name to play your own animation.
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

        if (transition.Transition.Start(transition.Direction) is not { } from)
        {
            // An unknown name must not stop the view from being shown.
            System.Diagnostics.Debug.WriteLine($"Routeables: no transition named '{name}' (register it with RouteTransitions.Register); the view appears at once.");
            return null;
        }

        if (from.TranslateX != 0)
            return new TransitionCollection { new EntranceThemeTransition { FromHorizontalOffset = from.TranslateX * Math.Max(40, width) } };
        if (from.TranslateY != 0)
            return new TransitionCollection { new EntranceThemeTransition { FromVerticalOffset = from.TranslateY * Math.Max(40, height) } };

        // A size or an angle: Uno does not implement PopupThemeTransition (Uno0001), so the view rises a little. A blur is not drawn,
        // so a view that only settles fades in.
        return name.Equals("Blur", StringComparison.OrdinalIgnoreCase) || name.Equals("Spotlight", StringComparison.OrdinalIgnoreCase) || !from.Moves
            ? new TransitionCollection { new ContentThemeTransition() }
            : new TransitionCollection { new EntranceThemeTransition { FromVerticalOffset = 24 } };
    }
}

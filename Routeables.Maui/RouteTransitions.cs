using System.Collections.Concurrent;
using Routeables.Abstraction;
using Routeables.Regions;

namespace Routeables.Maui;

/// <summary>
/// The transitions a <see cref="RouteableRegion"/> plays for a name (<see cref="RouteTransition"/>): the built-in ones
/// (<c>Fade</c>, <c>Slide</c>, <c>SlideUp</c>) and the custom ones registered here. The view that comes in is animated.
/// </summary>
public static class RouteTransitions
{
    private static readonly ConcurrentDictionary<string, Func<View, NavigationDirection, TimeSpan, Task>> Custom =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registers a transition under a name, then <c>args.WithTransition("Grow")</c> or <c>&lt;routeables:RouteableRegion Transition="Grow" /&gt;</c> plays it.
    /// A name that is a built-in one replaces it.
    /// </summary>
    /// <param name="name">The name of the transition.</param>
    /// <param name="play">Animates the region once its new view is shown: the way the history moved, and how long it should take.</param>
    public static void Register(string name, Func<View, NavigationDirection, TimeSpan, Task> play)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(play);
        Custom[name.Trim()] = play;
    }

    internal static async Task PlayAsync(View region, RegionTransition transition)
    {
        // Nothing asked for, nothing to touch: the region keeps the opacity and the translation it was given.
        if (transition.IsNone && !transition.Declared)
            return;

        // A new route change replaces the animation that is still running, and the region starts from rest.
        region.CancelAnimations();
        region.Opacity = 1;
        region.TranslationX = 0;
        region.TranslationY = 0;

        var name = transition.Transition.Name;
        try
        {
            if (Custom.TryGetValue(name, out var custom))
            {
                await custom(region, transition.Direction, transition.Duration);
                return;
            }

            if (transition.IsNone)
                return;

            var ms = (uint)transition.Duration.TotalMilliseconds;
            var sign = transition.Direction == NavigationDirection.Back ? -1 : 1;
            if (name.Equals("Fade", StringComparison.OrdinalIgnoreCase))
            {
                region.Opacity = 0;
                await region.FadeToAsync(1, ms);
            }
            else if (name.Equals("Slide", StringComparison.OrdinalIgnoreCase))
            {
                region.TranslationX = sign * Math.Max(24, region.Width * 0.25);
                region.Opacity = 0;
                await Task.WhenAll(region.TranslateToAsync(0, 0, ms, Easing.CubicOut), region.FadeToAsync(1, ms, Easing.CubicOut));
            }
            else if (name.Equals("SlideUp", StringComparison.OrdinalIgnoreCase))
            {
                region.TranslationY = sign * Math.Max(24, region.Height * 0.25);
                region.Opacity = 0;
                await Task.WhenAll(region.TranslateToAsync(0, 0, ms, Easing.CubicOut), region.FadeToAsync(1, ms, Easing.CubicOut));
            }
            else
            {
                // An unknown name must not stop the view from being shown.
                System.Diagnostics.Debug.WriteLine($"Routeables: no transition named '{name}' (register it with RouteTransitions.Register); the view appears at once.");
            }
        }
        catch (Exception)
        {
            // An animation that cannot run (the control left the page) must not leave the region invisible or off to the side.
            region.Opacity = 1;
            region.TranslationX = 0;
            region.TranslationY = 0;
        }
    }
}

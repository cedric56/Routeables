using System.Collections.Concurrent;
using Routeables.Abstraction;
using Routeables.Regions;

namespace Routeables.Maui;

/// <summary>
/// The transitions a <see cref="RouteableRegion"/> plays for a name (<see cref="RouteTransition"/>): the built-in ones
/// (<see cref="RouteTransition.BuiltIn"/>: <c>Fade</c>, <c>Slide</c>, <c>Pop</c>, <c>Flip</c>...) and the custom ones registered here. The view that
/// comes in is animated. A blur is not drawn: <c>Blur</c> and <c>Spotlight</c> fade in and settle.
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
        Rest(region);

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

            if (transition.Transition.Start(transition.Direction) is not { } from)
            {
                // An unknown name must not stop the view from being shown.
                System.Diagnostics.Debug.WriteLine($"Routeables: no transition named '{name}' (register it with RouteTransitions.Register); the view appears at once.");
                return;
            }

            var ms = (uint)transition.Duration.TotalMilliseconds;
            var easing = from.Overshoot ? Easing.SpringOut : Easing.CubicOut;

            // The view is put at the state the transition describes, then animated to rest.
            region.AnchorX = from.OriginX;
            region.AnchorY = from.OriginY;
            region.Opacity = from.Opacity;
            region.ScaleX = from.EffectiveScaleX;
            region.ScaleY = from.EffectiveScaleY;
            region.Rotation = from.Rotate;
            region.TranslationX = from.TranslateX == 0 ? 0 : from.TranslateX * Math.Max(24, region.Width);
            region.TranslationY = from.TranslateY == 0 ? 0 : from.TranslateY * Math.Max(24, region.Height);

            await Task.WhenAll(
                region.FadeToAsync(1, ms, Easing.CubicOut),
                region.ScaleXToAsync(1, ms, easing),
                region.ScaleYToAsync(1, ms, easing),
                region.RotateToAsync(0, ms, easing),
                region.TranslateToAsync(0, 0, ms, easing));
        }
        catch (Exception)
        {
            // An animation that cannot run (the control left the page) must not leave the region invisible or off to the side.
            Rest(region);
        }
    }

    // The region as it is when it is shown: whatever a transition did to it is undone.
    private static void Rest(View region)
    {
        region.Opacity = 1;
        region.Scale = 1;
        region.ScaleX = 1;
        region.ScaleY = 1;
        region.Rotation = 0;
        region.AnchorX = 0.5;
        region.AnchorY = 0.5;
        region.TranslationX = 0;
        region.TranslationY = 0;
    }
}

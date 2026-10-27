using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Routeables.Abstraction;
using Routeables.Regions;

namespace Routeables.Wpf;

/// <summary>
/// The transitions a <see cref="RouteableRegion"/> plays for a name (<see cref="RouteTransition"/>): the built-in ones
/// (<c>Fade</c>, <c>Slide</c>, <c>SlideUp</c>) and the custom ones registered here. The view that comes in is animated; the
/// transitions are not played when Windows has animations turned off.
/// </summary>
public static class RouteTransitions
{
    private static readonly ConcurrentDictionary<string, Action<FrameworkElement, NavigationDirection, TimeSpan>> Custom =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registers a transition under a name, then <c>args.WithTransition("Grow")</c> or <c>&lt;routeables:RouteableRegion Transition="Grow" /&gt;</c> plays it.
    /// A name that is a built-in one replaces it.
    /// </summary>
    /// <param name="name">The name of the transition.</param>
    /// <param name="play">Starts the animation on the region, once its new view is shown: the way the history moved, and how long it should take.</param>
    public static void Register(string name, Action<FrameworkElement, NavigationDirection, TimeSpan> play)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(play);
        Custom[name.Trim()] = play;
    }

    internal static void Play(FrameworkElement region, RegionTransition transition)
    {
        var name = transition.Transition.Name;
        if (Custom.TryGetValue(name, out var custom))
        {
            custom(region, transition.Direction, transition.Duration);
            return;
        }

        if (transition.IsNone || !SystemParameters.ClientAreaAnimation)
            return;

        var duration = new Duration(transition.Duration);
        var fade = new DoubleAnimation(0, 1, duration);

        if (name.Equals("Fade", StringComparison.OrdinalIgnoreCase))
        {
            region.BeginAnimation(UIElement.OpacityProperty, fade, HandoffBehavior.SnapshotAndReplace);
        }
        else if (name.Equals("Slide", StringComparison.OrdinalIgnoreCase) || name.Equals("SlideUp", StringComparison.OrdinalIgnoreCase))
        {
            var vertical = name.Equals("SlideUp", StringComparison.OrdinalIgnoreCase);
            var move = region.RenderTransform as TranslateTransform;
            if (move is null || move.IsFrozen)
            {
                // The region has a transform of its own: it is left alone, the view fades in.
                if (region.RenderTransform is not null && region.RenderTransform != Transform.Identity)
                {
                    region.BeginAnimation(UIElement.OpacityProperty, fade, HandoffBehavior.SnapshotAndReplace);
                    return;
                }

                region.RenderTransform = move = new TranslateTransform();
            }

            var size = vertical ? region.ActualHeight : region.ActualWidth;
            var distance = Math.Max(24, size * 0.25) * (transition.Direction == NavigationDirection.Back ? -1 : 1);
            var slide = new DoubleAnimation(distance, 0, duration) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            move.BeginAnimation(vertical ? TranslateTransform.YProperty : TranslateTransform.XProperty, slide, HandoffBehavior.SnapshotAndReplace);
            region.BeginAnimation(UIElement.OpacityProperty, fade, HandoffBehavior.SnapshotAndReplace);
        }
        else
        {
            // An unknown name must not stop the view from being shown.
            System.Diagnostics.Debug.WriteLine($"Routeables: no transition named '{name}' (register it with RouteTransitions.Register); the view appears at once.");
        }
    }
}

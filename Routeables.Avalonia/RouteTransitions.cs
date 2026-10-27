using System.Collections.Concurrent;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Routeables.Abstraction;
using Routeables.Regions;

namespace Routeables.Avalonia;

/// <summary>
/// The transitions a <see cref="RouteableRegion"/> plays for a name (<see cref="RouteTransition"/>): the built-in ones
/// (<c>Fade</c>, <c>Slide</c>, <c>SlideUp</c>) and the custom ones registered here. A built-in transition animates the opacity and the
/// <c>RenderTransform</c> of the view that comes in, so the view must not rely on its own <c>RenderTransform</c>.
/// </summary>
public static class RouteTransitions
{
    private static readonly ConcurrentDictionary<string, Func<NavigationDirection, TimeSpan, IPageTransition?>> Custom =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registers a transition under a name: <c>RouteTransitions.Register("Spin", (direction, duration) =&gt; new Rotate3DTransition(duration))</c>,
    /// then <c>args.WithTransition("Spin")</c> or <c>&lt;routeables:RouteableRegion Transition="Spin" /&gt;</c>. A name that is a built-in one replaces it.
    /// </summary>
    /// <param name="name">The name of the transition.</param>
    /// <param name="create">Builds the transition for a change: the way the history moved, and how long it should take.</param>
    public static void Register(string name, Func<NavigationDirection, TimeSpan, IPageTransition?> create)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(create);
        Custom[name.Trim()] = create;
    }

    /// <summary>The transition registered under that name with <see cref="Register"/>, or <see langword="null"/> for a built-in one.</summary>
    internal static IPageTransition? CreateCustom(RegionTransition transition)
        => Custom.TryGetValue(transition.Transition.Name, out var create) ? create(transition.Direction, transition.Duration) : null;

    internal static bool IsCustom(string name) => Custom.ContainsKey(name);

    // The built-in transitions animate the view that comes in (opacity, and a translation), not the two presenters of the
    // TransitioningContentControl: the control swaps the content at once, and the view is brought to rest by the transitions of Avalonia.
    // The view starts transparent and off to the side; the next frame sets it to rest, which the transitions animate.
    internal static void AnimateIn(Visual view, RegionTransition transition, Size bounds)
    {
        var name = transition.Transition.Name;
        var fade = name.Equals("Fade", StringComparison.OrdinalIgnoreCase);
        var slide = name.Equals("Slide", StringComparison.OrdinalIgnoreCase);
        var slideUp = name.Equals("SlideUp", StringComparison.OrdinalIgnoreCase);
        if (!fade && !slide && !slideUp)
        {
            // An unknown name must not stop the view from being shown.
            System.Diagnostics.Debug.WriteLine($"Routeables: no transition named '{name}' (register it with RouteTransitions.Register); the view appears at once.");
            return;
        }

        var sign = transition.Direction == NavigationDirection.Back ? -1 : 1;
        string? start = null;
        if (slide)
            start = FormattableString.Invariant($"translateX({sign * Math.Max(24, bounds.Width * 0.25):0}px)");
        else if (slideUp)
            start = FormattableString.Invariant($"translateY({sign * Math.Max(24, bounds.Height * 0.25):0}px)");

        view.Transitions = null;
        view.Opacity = 0;
        if (start is not null)
            view.RenderTransform = TransformOperations.Parse(start);

        // After the frame drawn with the start values: from now on a change of Opacity or RenderTransform is animated.
        Dispatcher.UIThread.Post(() =>
        {
            var transitions = new Transitions
            {
                new DoubleTransition { Property = Visual.OpacityProperty, Duration = transition.Duration }
            };
            if (start is not null)
                transitions.Add(new TransformOperationsTransition { Property = Visual.RenderTransformProperty, Duration = transition.Duration, Easing = new CubicEaseOut() });

            view.Transitions = transitions;
            view.Opacity = 1;
            if (start is not null)
                view.RenderTransform = TransformOperations.Identity;
        }, DispatcherPriority.Background);
    }
}

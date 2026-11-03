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
/// (<see cref="RouteTransition.BuiltIn"/>: <c>Fade</c>, <c>Slide</c>, <c>Pop</c>, <c>Flip</c>...) and the custom ones registered here. A blur is not drawn: <c>Blur</c> and <c>Spotlight</c> fade in and settle. A built-in transition animates the opacity and the
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

    // The built-in transitions animate the view that comes in (opacity, and a transform that goes to rest), not the two presenters of the
    // TransitioningContentControl: the control swaps the content at once, and the view is brought to rest by the transitions of Avalonia.
    // The view starts at the state the transition describes (RouteTransition.Start); the next frame sets it to rest, which the transitions animate.
    internal static void AnimateIn(Visual view, RegionTransition transition, Size bounds)
    {
        var name = transition.Transition.Name;
        if (transition.Transition.Start(transition.Direction) is not { } from)
        {
            // An unknown name must not stop the view from being shown.
            System.Diagnostics.Debug.WriteLine($"Routeables: no transition named '{name}' (register it with RouteTransitions.Register); the view appears at once.");
            return;
        }

        // A view that is scaled to nothing cannot be interpolated: the smallest size is a sliver.
        string? start = null;
        if (from.Moves)
        {
            var x = from.TranslateX == 0 ? 0 : from.TranslateX * Math.Max(24, bounds.Width);
            var y = from.TranslateY == 0 ? 0 : from.TranslateY * Math.Max(24, bounds.Height);
            start = FormattableString.Invariant(
                $"translate({x:0.##}px, {y:0.##}px) scale({Math.Max(from.EffectiveScaleX, 0.01):0.###}, {Math.Max(from.EffectiveScaleY, 0.01):0.###}) rotate({from.Rotate:0.##}deg)");
        }

        view.Transitions = null;
        view.Opacity = from.Opacity;
        view.RenderTransformOrigin = new RelativePoint(from.OriginX, from.OriginY, RelativeUnit.Relative);
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
            {
                transitions.Add(new TransformOperationsTransition
                {
                    Property = Visual.RenderTransformProperty,
                    Duration = transition.Duration,
                    Easing = from.Overshoot ? new BackEaseOut() : new CubicEaseOut()
                });
            }

            view.Transitions = transitions;
            view.Opacity = 1;
            if (start is not null)
                view.RenderTransform = TransformOperations.Identity;
        }, DispatcherPriority.Background);
    }
}

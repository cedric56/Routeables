using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Routeables.Abstraction;
using Routeables.Regions;

namespace Routeables.Wpf;

/// <summary>
/// The transitions a <see cref="RouteableRegion"/> plays for a name (<see cref="RouteTransition"/>): the built-in ones
/// (<see cref="RouteTransition.BuiltIn"/>: <c>Fade</c>, <c>Slide</c>, <c>Pop</c>, <c>Flip</c>...) and the custom ones registered here. The view that
/// comes in is animated; the transitions are not played when Windows has animations turned off. A blur is not drawn: <c>Blur</c> and
/// <c>Spotlight</c> fade in and settle.
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

        if (transition.Transition.Start(transition.Direction) is not { } from)
        {
            // An unknown name must not stop the view from being shown.
            System.Diagnostics.Debug.WriteLine($"Routeables: no transition named '{name}' (register it with RouteTransitions.Register); the view appears at once.");
            return;
        }

        var duration = new Duration(transition.Duration);
        var fade = new DoubleAnimation(from.Opacity, 1, duration);
        var easing = from.Overshoot
            ? (IEasingFunction)new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.5 }
            : new CubicEase { EasingMode = EasingMode.EaseOut };

        // The region has a transform of its own: it is left alone, the view fades in.
        var own = region.RenderTransform;
        var ours = Owned.TryGetValue(region, out var group) && ReferenceEquals(own, group);
        if (!from.Moves || (!ours && own is not null && own != Transform.Identity))
        {
            region.BeginAnimation(UIElement.OpacityProperty, fade, HandoffBehavior.SnapshotAndReplace);
            return;
        }

        if (!ours)
        {
            group = new TransformGroup { Children = { new ScaleTransform(), new RotateTransform(), new TranslateTransform() } };
            Owned.Remove(region);
            Owned.Add(region, group);
            region.RenderTransform = group;
        }

        var scale = (ScaleTransform)group!.Children[0];
        var rotate = (RotateTransform)group.Children[1];
        var move = (TranslateTransform)group.Children[2];
        region.RenderTransformOrigin = new Point(from.OriginX, from.OriginY);

        scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(from.EffectiveScaleX, 1, duration) { EasingFunction = easing }, HandoffBehavior.SnapshotAndReplace);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(from.EffectiveScaleY, 1, duration) { EasingFunction = easing }, HandoffBehavior.SnapshotAndReplace);
        rotate.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(from.Rotate, 0, duration) { EasingFunction = easing }, HandoffBehavior.SnapshotAndReplace);
        var x = from.TranslateX == 0 ? 0 : from.TranslateX * Math.Max(24, region.ActualWidth);
        var y = from.TranslateY == 0 ? 0 : from.TranslateY * Math.Max(24, region.ActualHeight);
        move.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(x, 0, duration) { EasingFunction = easing }, HandoffBehavior.SnapshotAndReplace);
        move.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(y, 0, duration) { EasingFunction = easing }, HandoffBehavior.SnapshotAndReplace);
        region.BeginAnimation(UIElement.OpacityProperty, fade, HandoffBehavior.SnapshotAndReplace);
    }

    // The transform group a region got from a transition: the one that is the RenderTransform of the region is ours, any other is the region's own.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<FrameworkElement, TransformGroup> Owned = new();
}

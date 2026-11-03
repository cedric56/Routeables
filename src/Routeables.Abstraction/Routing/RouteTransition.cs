namespace Routeables.Abstraction;

/// <summary>Which way a route change moves in the history: what a transition needs to slide the right way.</summary>
public enum NavigationDirection
{
    /// <summary>No direction: the history was emptied or replaced, or the route change is not known.</summary>
    None,
    /// <summary>A new page, or the cursor moving forward: the new view comes in from the right (or from below).</summary>
    Forward,
    /// <summary>The cursor moving back (or the history going back to its root): the new view comes in from the left (or from above).</summary>
    Back
}

/// <summary>
/// How a region brings in the view of a route change: a name, and optionally how long it takes. It carries no animation code: each
/// host (Avalonia, WPF, Uno, MAUI, Razor) plays the animation of that name with its own tools.
/// <para>
/// The built-in set is <see cref="None"/>, <see cref="Fade"/>, <see cref="Slide"/> (from the side, the way the history moves),
/// <see cref="SlideUp"/>, <see cref="SlideFade"/>, <see cref="Swipe"/>, <see cref="Pop"/>, <see cref="Scale"/>, <see cref="Lift"/>,
/// <see cref="Blur"/>, <see cref="Spotlight"/>, <see cref="Flip"/>, <see cref="Rotate"/>, <see cref="Reveal"/> and <see cref="Curtain"/>.
/// Each host plays them with its own tools, as close to the description as it can (Razor, with CSS, is the closest; the others
/// approximate what they cannot draw, such as a blur or a 3D rotation: see the transitions guide). Any other name is a custom transition, which each host plays if it was registered
/// (<c>RouteTransitions.Register</c> of the host; a CSS class <c>rt-name</c> in Razor).
/// </para>
/// <para>
/// Choose it for one navigation (<c>RouteableArgs.Create().WithTransition(RouteTransition.Slide)</c>) or for a region
/// (<c>&lt;RouteableRegion Transition="Fade" /&gt;</c>); the navigation wins.
/// </para>
/// </summary>
public sealed class RouteTransition : IEquatable<RouteTransition>
{
    /// <summary>The length of a transition that does not give its own, when the region gives none either.</summary>
    public static readonly TimeSpan DefaultDuration = TimeSpan.FromMilliseconds(250);

    private RouteTransition(string name, TimeSpan? duration)
    {
        Name = name;
        Duration = duration;
    }

    /// <summary>The view appears at once.</summary>
    public static RouteTransition None { get; } = new("None", null);

    /// <summary>The new view fades in.</summary>
    public static RouteTransition Fade { get; } = new("Fade", null);

    /// <summary>The new view comes in from the side (from the right when going forward, from the left when going back), fading in.</summary>
    public static RouteTransition Slide { get; } = new("Slide", null);

    /// <summary>The new view comes in from below when going forward, from above when going back, fading in.</summary>
    public static RouteTransition SlideUp { get; } = new("SlideUp", null);

    /// <summary>The new view comes in from the side like <see cref="Slide"/>, over a short distance: a light movement, fading in.</summary>
    public static RouteTransition SlideFade { get; } = new("SlideFade", null);

    /// <summary>The new view crosses the whole region from the right when going forward, from the left when going back, like a swipe on a phone.</summary>
    public static RouteTransition Swipe { get; } = new("Swipe", null);

    /// <summary>The new view grows from a smaller size with a small overshoot, fading in.</summary>
    public static RouteTransition Pop { get; } = new("Pop", null);

    /// <summary>The new view grows from a smaller size to its size, fading in.</summary>
    public static RouteTransition Scale { get; } = new("Scale", null);

    /// <summary>The new view rises a little, from slightly smaller, fading in: a card that is put down.</summary>
    public static RouteTransition Lift { get; } = new("Lift", null);

    /// <summary>The new view goes from blurred to sharp, fading in.</summary>
    public static RouteTransition Blur { get; } = new("Blur", null);

    /// <summary>The new view goes from dim and blurred to clear, settling from slightly larger.</summary>
    public static RouteTransition Spotlight { get; } = new("Spotlight", null);

    /// <summary>The new view turns around its vertical axis, like a card being flipped.</summary>
    public static RouteTransition Flip { get; } = new("Flip", null);

    /// <summary>The new view turns in around its centre, growing, fading in.</summary>
    public static RouteTransition Rotate { get; } = new("Rotate", null);

    /// <summary>The new view is uncovered from the bottom to the top.</summary>
    public static RouteTransition Reveal { get; } = new("Reveal", null);

    /// <summary>The new view is uncovered from the top to the bottom, like a curtain being raised.</summary>
    public static RouteTransition Curtain { get; } = new("Curtain", null);

    /// <summary>The name of the transition, which is what a host plays or looks up.</summary>
    public string Name { get; }

    /// <summary>How long it takes; <see langword="null"/> takes the length of the region, else <see cref="DefaultDuration"/>.</summary>
    public TimeSpan? Duration { get; }

    /// <summary>Every built-in transition.</summary>
    public static IReadOnlyList<RouteTransition> BuiltIn { get; } =
        [None, Fade, Slide, SlideUp, SlideFade, Swipe, Pop, Scale, Lift, Blur, Spotlight, Flip, Rotate, Reveal, Curtain];

    /// <summary>Whether this is one of the <see cref="BuiltIn"/> transitions (a custom one is played by what was registered under its name).</summary>
    public bool IsBuiltIn => BuiltIn.Any(b => string.Equals(b.Name, Name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Where the view comes from, for a host that plays a built-in transition by animating the new view from this state to rest
    /// (<see cref="RouteTransitionStart.Rest"/>).
    /// </summary>
    /// <param name="direction">The way the history moved: it decides which side a slide starts from.</param>
    /// <returns>The starting state, or <see langword="null"/> for <see cref="None"/> and for a custom transition.</returns>
    public RouteTransitionStart? Start(NavigationDirection direction)
    {
        var s = direction == NavigationDirection.Back ? -1.0 : 1.0;
        return Name.ToUpperInvariant() switch
        {
            "FADE" => new(),
            "SLIDE" => new() { TranslateX = s * 0.25 },
            "SLIDEUP" => new() { TranslateY = s * 0.25 },
            "SLIDEFADE" => new() { TranslateX = s * 0.08 },
            "SWIPE" => new() { Opacity = 1, TranslateX = s },
            "POP" => new() { Scale = 0.85, Overshoot = true },
            "SCALE" => new() { Scale = 0.8 },
            "LIFT" => new() { Scale = 0.97, TranslateY = 0.04 },
            "BLUR" => new() { Scale = 1.03 },
            "SPOTLIGHT" => new() { Scale = 1.06 },
            "FLIP" => new() { ScaleX = 0.02 },
            "ROTATE" => new() { Scale = 0.7, Rotate = -90 },
            "REVEAL" => new() { Opacity = 1, ScaleY = 0.02, OriginY = 1 },
            "CURTAIN" => new() { Opacity = 1, ScaleY = 0.02, OriginY = 0 },
            _ => null
        };
    }

    /// <summary>Whether the view appears at once.</summary>
    public bool IsNone => string.Equals(Name, "None", StringComparison.OrdinalIgnoreCase);

    /// <summary>A copy of this transition with its own length.</summary>
    /// <param name="duration">How long it takes.</param>
    public RouteTransition WithDuration(TimeSpan duration) => new(Name, duration);

    /// <summary>
    /// The transition of that name: a built-in one (without regard to case), otherwise a custom one.
    /// A blank name is <see cref="None"/>.
    /// </summary>
    /// <param name="name">The name, such as <c>Fade</c> or the name a custom transition was registered under.</param>
    public static RouteTransition Named(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return None;

        name = name.Trim();
        foreach (var builtIn in BuiltIn)
            if (string.Equals(builtIn.Name, name, StringComparison.OrdinalIgnoreCase))
                return builtIn;

        return new RouteTransition(name, null);
    }

    /// <summary>A name is a transition: <c>args.WithTransition("Slide")</c>.</summary>
    public static implicit operator RouteTransition(string name) => Named(name);

    /// <inheritdoc />
    public bool Equals(RouteTransition? other)
        => other is not null && string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase) && Duration == other.Duration;

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as RouteTransition);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Name.ToUpperInvariant(), Duration);

    /// <inheritdoc />
    public override string ToString() => Duration is { } d ? $"{Name} ({d.TotalMilliseconds:0} ms)" : Name;
}

/// <summary>
/// The state a built-in <see cref="RouteTransition"/> starts from: the host puts the new view there, then animates it to
/// <see cref="Rest"/> (opacity 1, no scale, no translation, no rotation). It is the same description for every host, so the
/// transitions look alike; what a host cannot draw (a blur, a 3D rotation) it leaves out.
/// </summary>
public sealed record RouteTransitionStart
{
    /// <summary>The state of a view that is shown: nothing to animate.</summary>
    public static RouteTransitionStart Rest { get; } = new() { Opacity = 1 };

    /// <summary>The opacity the view starts from: 0 (it fades in) by default.</summary>
    public double Opacity { get; init; }

    /// <summary>The size it starts from, as a fraction of its size: 1 is its size.</summary>
    public double Scale { get; init; } = 1;

    /// <summary>The width it starts from (as <see cref="Scale"/>, on the horizontal axis only), multiplied with <see cref="Scale"/>.</summary>
    public double ScaleX { get; init; } = 1;

    /// <summary>The height it starts from (as <see cref="Scale"/>, on the vertical axis only), multiplied with <see cref="Scale"/>.</summary>
    public double ScaleY { get; init; } = 1;

    /// <summary>The horizontal offset it starts from, as a fraction of the width of the region (positive: to the right).</summary>
    public double TranslateX { get; init; }

    /// <summary>The vertical offset it starts from, as a fraction of the height of the region (positive: below).</summary>
    public double TranslateY { get; init; }

    /// <summary>The rotation it starts from, in degrees, clockwise.</summary>
    public double Rotate { get; init; }

    /// <summary>The point the scale and the rotation turn around, as a fraction of the width (0 left, 1 right).</summary>
    public double OriginX { get; init; } = 0.5;

    /// <summary>The point the scale and the rotation turn around, as a fraction of the height (0 top, 1 bottom).</summary>
    public double OriginY { get; init; } = 0.5;

    /// <summary>Whether the movement goes a little past the rest state before it settles (a bounce).</summary>
    public bool Overshoot { get; init; }

    /// <summary>The size on the horizontal axis: <see cref="Scale"/> times <see cref="ScaleX"/>.</summary>
    public double EffectiveScaleX => Scale * ScaleX;

    /// <summary>The size on the vertical axis: <see cref="Scale"/> times <see cref="ScaleY"/>.</summary>
    public double EffectiveScaleY => Scale * ScaleY;

    /// <summary>Whether the view starts away from rest in size, place or angle (and not only in opacity).</summary>
    public bool Moves => EffectiveScaleX != 1 || EffectiveScaleY != 1 || TranslateX != 0 || TranslateY != 0 || Rotate != 0;
}

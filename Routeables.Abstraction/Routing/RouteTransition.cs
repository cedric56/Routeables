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
/// The built-in set is <see cref="None"/>, <see cref="Fade"/>, <see cref="Slide"/> (from the side, the way the history moves) and
/// <see cref="SlideUp"/>. Any other name is a custom transition, which each host plays if it was registered
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

    /// <summary>The name of the transition, which is what a host plays or looks up.</summary>
    public string Name { get; }

    /// <summary>How long it takes; <see langword="null"/> takes the length of the region, else <see cref="DefaultDuration"/>.</summary>
    public TimeSpan? Duration { get; }

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
        foreach (var builtIn in new[] { None, Fade, Slide, SlideUp })
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

using Routeables.Abstraction;
using Routeables.Abstraction.Transitions;
using Routeables.Regions;

namespace Routeables.AspNetCore;

/// <summary>
/// The transition a region brings its view in with, for a host that sends whole pages: each request writes the view again, so the page
/// that is shown is a new element and the browser plays the CSS animation when it loads. It is the same description as on the other hosts
/// (<see cref="RouteTransition"/>, <see cref="IRouteableArgs.Transition"/>): the transition of the navigation wins over the one of the region.
/// </summary>
/// <remarks>
/// <para>Call <see cref="Next"/> where the region is written, with the transition the region declares. The element that holds the view gets
/// <see cref="RouteableTransitionStyle.CssClass"/> and <see cref="RouteableTransitionStyle.Style"/>; the animations of the built-in names are in
/// <see cref="Css"/> (put it in a <c>&lt;style&gt;</c> or copy it into your stylesheet), and a custom name is a CSS animation you write for
/// <c>.rt-enter.rt-yourname</c>.</para>
/// <para>A transition plays once, for the request that follows the route change: a reload of the same page, or the second time the region is
/// written for the same change, plays none. The first page of a region, shown before anything was routed, comes in without one. The
/// service lives in the scope of the visitor (as the routers do) and follows each region from the first time <see cref="Next"/> is called for it.</para>
/// </remarks>
public sealed class RouteableTransitions(IServiceProvider provider) : IDisposable
{
    private readonly Dictionary<string, Follow> _regions = [];
    private readonly object _gate = new();
    private bool _disposed;

    /// <summary>The CSS of the built-in transitions: the animations <c>rt-*</c> and the class <c>rt-enter</c>.</summary>
    public static string Css { get; } = LoadCss();

    /// <summary>
    /// What the element that holds the view of <paramref name="region"/> gets for the change that put the view there.
    /// </summary>
    /// <param name="region">The region (<see langword="null"/> for the default router).</param>
    /// <param name="regionTransition">The transition the region declares, used when the navigation did not ask for one.</param>
    /// <param name="regionDuration">The length the region declares; zero for the default length of the transition.</param>
    public RouteableTransitionStyle Next(string? region = null, RouteTransition? regionTransition = null, TimeSpan regionDuration = default)
    {
        Follow follow;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var key = region ?? string.Empty;
            if (!_regions.TryGetValue(key, out follow!))
                _regions[key] = follow = new Follow(provider.GetRouter(region));
        }

        var change = follow.Take();
        if (change is null)
            return RouteableTransitionStyle.None;

        var chosen = change.Resolve(string.IsNullOrWhiteSpace(regionTransition?.Name) ? null : regionTransition, regionDuration);
        return chosen.IsNone ? RouteableTransitionStyle.None : RouteableTransitionStyle.Of(chosen);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            foreach (var follow in _regions.Values)
                follow.Dispose();
            _regions.Clear();
        }
    }

    // What one region last showed, and whether that has been written yet.
    private sealed class Follow : IDisposable
    {
        private readonly RegionBinding _binding;
        private readonly object _lock = new();
        private RegionChange? _unread;
        private bool _started;

        public Follow(IRouter router)
        {
            _binding = new RegionBinding(router, Show);   // calls Show at once with the view the router shows now
            lock (_lock)
                _started = true;
        }

        private void Show(object? view, RegionChange change)
        {
            lock (_lock)
                _unread = _started && view is not null ? change : null;     // the view that was already there comes in without one
        }

        public RegionChange? Take()
        {
            lock (_lock)
            {
                var change = _unread;
                _unread = null;
                return change;
            }
        }

        public void Dispose() => _binding.Dispose();
    }

    private static string LoadCss()
    {
        var assembly = typeof(RouteableTransitions).Assembly;
        using var stream = assembly.GetManifestResourceStream("Routeables.AspNetCore.transitions.css")
                           ?? throw new InvalidOperationException("The stylesheet of the transitions is not in the assembly.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

/// <summary>The class and the style of the element that holds a view brought in with a transition.</summary>
/// <param name="CssClass">The classes: <c>rt-enter rt-{name} rt-{forward|back|none}</c>; empty when the view comes in at once.</param>
/// <param name="Style">The style: <c>--rt-duration:250ms</c>; empty when the view comes in at once.</param>
public readonly record struct RouteableTransitionStyle(string CssClass, string Style)
{
    /// <summary>The view comes in at once.</summary>
    public static RouteableTransitionStyle None { get; } = new(string.Empty, string.Empty);

    /// <summary>Whether a transition plays.</summary>
    public bool Animated => CssClass.Length > 0;

    internal static RouteableTransitionStyle Of(RegionTransition transition)
    {
        var direction = transition.Direction == RouteableDirection.Back ? "back"
            : transition.Direction == RouteableDirection.Forward ? "forward" : "none";
        var name = new string(transition.Transition.Name.ToLowerInvariant()
            .Select(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' ? c : '-').ToArray());
        return new($"rt-enter rt-{name} rt-{direction}", $"--rt-duration:{(int)transition.Duration.TotalMilliseconds}ms");
    }
}

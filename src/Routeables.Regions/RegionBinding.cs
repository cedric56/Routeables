using Routeables.Abstraction;

namespace Routeables.Regions;

/// <summary>
/// What a region host (the <c>RouteableRegion</c> of each platform) does, without any UI: shows the current view of a
/// router, and again each time the history of that router changes.
/// <para>
/// A region is a router with its own history. Whoever routes it (a view model, a menu entry whose
/// <see cref="IRouteableEntry.Region"/> names it, a URL segment <c>region:name</c>) never says where on screen the view goes:
/// the host that is given the same name receives it. Several hosts can display the same region.
/// </para>
/// <para>
/// <c>show</c> is called with the new view on the thread that routed (the host moves it to the UI thread), only when the
/// view changed, and first with the view the router shows now. It is called while the router holds its gate: it must display
/// the view and nothing else, as any handler of <see cref="IRouterHistory.Changed"/>.
/// </para>
/// </summary>
public sealed class RegionBinding : IDisposable
{
    private readonly Action<object?, RegionChange> _show;
    private RegionChange? _pending;
    private readonly object _lock = new();
    private object? _shown;
    private bool _hasShown;
    private bool _disposed;

    /// <summary>Follows <paramref name="router"/> and calls <paramref name="show"/> with its current view, now and after each change.</summary>
    /// <param name="router">The router of the region.</param>
    /// <param name="show">Shows the view; called on the UI thread of the host.</param>
    public RegionBinding(IRouter router, Action<object?> show)
        : this(router, Adapt(show))
    {
    }

    /// <summary>
    /// Follows <paramref name="router"/> like the other constructor, and tells <paramref name="show"/> which way the history moved and which
    /// transition the navigation asked for (<see cref="IRouteableArgs.Transition"/>).
    /// </summary>
    /// <param name="router">The router of the region.</param>
    /// <param name="show">Shows the view with the change that brought it; called on the UI thread of the host.</param>
    public RegionBinding(IRouter router, Action<object?, RegionChange> show)
    {
        ArgumentNullException.ThrowIfNull(router);
        ArgumentNullException.ThrowIfNull(show);
        Router = router;
        _show = show;
        router.Routing += OnRouting;
        router.Routed += OnRouted;
        router.History.Changed += OnChanged;
        Refresh();
    }

    private static Action<object?, RegionChange> Adapt(Action<object?> show)
    {
        ArgumentNullException.ThrowIfNull(show);
        return (view, _) => show(view);
    }

    /// <summary>The router of the region.</summary>
    public IRouter Router { get; }

    /// <summary>Shows the current view of the router if it is not the one already shown.</summary>
    public void Refresh()
    {
        object? view;
        RegionChange change;
        lock (_lock)
        {
            if (_disposed) return;
            view = Router.CurrentView;
            if (_hasShown && ReferenceEquals(view, _shown)) return;
            _hasShown = true;
            _shown = view;
            change = _pending ?? RegionChange.Unknown;
        }

        _show(view, change);
    }

    private void OnChanged(object? sender, EventArgs e) => Refresh();

    // A route change raises Routing, then changes the history (Changed), then raises Routed: what Routing says is what Changed shows.
    private void OnRouting(object? sender, RouteableRoutingEventArgs e)
    {
        var change = new RegionChange(DirectionOf(e), e.To.Args.Transition);
        lock (_lock) _pending = change;
    }

    private void OnRouted(object? sender, RouteableRoutedEventArgs e)
    {
        lock (_lock) _pending = null;
    }

    private NavigationDirection DirectionOf(RouteableRoutingEventArgs e)
    {
        switch (e.Kind)
        {
            case RouteableKind.Push:
            case RouteableKind.Forward:
                return NavigationDirection.Forward;
            case RouteableKind.Back:
            case RouteableKind.Pop:
            case RouteableKind.Root:
                return NavigationDirection.Back;
            case RouteableKind.MoveTo:
                var entries = Router.History.Entries;
                var from = e.From is null ? -1 : IndexOf(entries, e.From);
                var to = IndexOf(entries, e.To);
                return from < 0 || to < 0 || from == to ? NavigationDirection.None
                     : to > from ? NavigationDirection.Forward : NavigationDirection.Back;
            default:
                return NavigationDirection.None;
        }
    }

    private static int IndexOf(IReadOnlyList<IHistoryEntry> entries, IHistoryEntry entry)
    {
        for (var i = 0; i < entries.Count; i++)
            if (ReferenceEquals(entries[i], entry))
                return i;
        return -1;
    }

    /// <summary>Stops listening: the router no longer keeps the host alive.</summary>
    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
            _shown = null;
        }

        Router.History.Changed -= OnChanged;
        Router.Routing -= OnRouting;
        Router.Routed -= OnRouted;
    }
}

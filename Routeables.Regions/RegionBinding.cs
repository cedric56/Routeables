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
    private readonly Action<object?> _show;
    private readonly object _lock = new();
    private object? _shown;
    private bool _hasShown;
    private bool _disposed;

    public RegionBinding(IRouter router, Action<object?> show)
    {
        ArgumentNullException.ThrowIfNull(router);
        ArgumentNullException.ThrowIfNull(show);
        Router = router;
        _show = show;
        router.History.Changed += OnChanged;
        Refresh();
    }

    /// <summary>The router of the region.</summary>
    public IRouter Router { get; }

    /// <summary>Shows the current view of the router if it is not the one already shown.</summary>
    public void Refresh()
    {
        object? view;
        lock (_lock)
        {
            if (_disposed) return;
            view = Router.CurrentView;
            if (_hasShown && ReferenceEquals(view, _shown)) return;
            _hasShown = true;
            _shown = view;
        }

        _show(view);
    }

    private void OnChanged(object? sender, EventArgs e) => Refresh();

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
    }
}

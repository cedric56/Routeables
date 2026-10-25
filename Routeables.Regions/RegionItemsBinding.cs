using Routeables.Abstraction;

namespace Routeables.Regions;

/// <summary>
/// What a region host that shows <b>every</b> view of a region does, without any UI (<see cref="RegionBinding"/> shows only the
/// current one): gives the views of the history, oldest first, up to the current one, and again each time that list changes.
/// <para>
/// A region is a router with its own history, so <c>PushAsync</c> adds a view to the list, <c>PopAsync</c> and <c>BackAsync</c>
/// remove the last one, and the views a <c>ForwardAsync</c> could reach are not in the list. The last view is the current one.
/// </para>
/// <para>
/// <c>show</c> is called with the list on the thread that routed (the host moves it to the UI thread), only when the list
/// changed, and first with the list of now. It is called while the router holds its gate: it must display the views and
/// nothing else, as any handler of <see cref="IRouterHistory.Changed"/>.
/// </para>
/// </summary>
public sealed class RegionItemsBinding : IDisposable
{
    private readonly Action<IReadOnlyList<object>> _show;
    private readonly object _lock = new();
    private IReadOnlyList<object>? _shown;
    private bool _disposed;

    public RegionItemsBinding(IRouter router, Action<IReadOnlyList<object>> show)
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

    /// <summary>Gives the views of the router if they are not the ones already given.</summary>
    public void Refresh()
    {
        IReadOnlyList<object> views;
        lock (_lock)
        {
            if (_disposed) return;
            views = ViewsOf(Router.History);
            if (_shown is not null && _shown.SequenceEqual(views, ReferenceEqualityComparer.Instance)) return;
            _shown = views;
        }

        _show(views);
    }

    private static IReadOnlyList<object> ViewsOf(IRouterHistory history)
    {
        var entries = history.Entries;
        var count = Math.Min(entries.Count, history.CurrentIndex + 1);
        var views = new List<object>(Math.Max(count, 0));
        for (var i = 0; i < count; i++)
            views.Add(entries[i].View);
        return views;
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

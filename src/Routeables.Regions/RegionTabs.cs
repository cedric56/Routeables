using Routeables.Abstraction.Routing;

namespace Routeables.Regions;

/// <summary>
/// What <c>AttachRouteables</c> does for a tab control of any platform, without any UI: one region host moved to the selected tab, the route of
/// a tab chosen by the user, and the tab of a route that something else reached (Back, a link, code).
/// <para>
/// The platform tells how to read and write a tab (<c>TabItem</c>) through delegates, and calls <see cref="OnSelectionChanged"/> for the selection
/// of the tab control itself (the selections of the controls inside the pages bubble up to it: they are not for here).
/// </para>
/// </summary>
/// <typeparam name="TTab">The tab type of the platform.</typeparam>
public sealed class RegionTabs<TTab> where TTab : class
{
    private readonly IServiceProvider _provider;
    private readonly IRouter _router;
    private readonly object _host;
    private readonly IReadOnlyList<TTab> _owned;
    private readonly Func<TTab, string?> _routeOf;
    private readonly Func<TTab, object?> _contentOf;
    private readonly Action<TTab, object?> _setContent;
    private readonly Func<TTab?> _selected;
    private readonly Action<TTab> _select;
    private readonly Action<Action> _post;
    private readonly Type _owner;

    /// <summary>Creates the follower; nothing happens until <see cref="Start"/>.</summary>
    /// <param name="provider">The container of the host.</param>
    /// <param name="region">The region whose history the tabs have; the default router when <see langword="null"/>.</param>
    /// <param name="host">The region host (<c>RouteableRegion</c>), one for all tabs: it is the content of the selected one.</param>
    /// <param name="tabs">Every tab of the control, in order.</param>
    /// <param name="routeOf">The route of a tab (<c>RouteableItem.Route</c>); a tab without one is left alone.</param>
    /// <param name="contentOf">The content of a tab; a tab with a content of its own is left alone.</param>
    /// <param name="setContent">Sets the content of a tab.</param>
    /// <param name="selected">The selected tab, or <see langword="null"/>.</param>
    /// <param name="select">Selects a tab.</param>
    /// <param name="post">Runs an action on the UI thread, after the current one.</param>
    /// <param name="owner">The type that asks, named in the report of an error.</param>
    public RegionTabs(IServiceProvider provider, string? region, object host, IEnumerable<TTab> tabs,
        Func<TTab, string?> routeOf, Func<TTab, object?> contentOf, Action<TTab, object?> setContent,
        Func<TTab?> selected, Action<TTab> select, Action<Action> post, Type owner)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(tabs);
        ArgumentNullException.ThrowIfNull(routeOf);
        ArgumentNullException.ThrowIfNull(contentOf);
        ArgumentNullException.ThrowIfNull(setContent);
        ArgumentNullException.ThrowIfNull(selected);
        ArgumentNullException.ThrowIfNull(select);
        ArgumentNullException.ThrowIfNull(post);
        ArgumentNullException.ThrowIfNull(owner);

        _provider = provider;
        _router = provider.GetRouter(region);
        _host = host;
        _routeOf = routeOf;
        _contentOf = contentOf;
        _setContent = setContent;
        _selected = selected;
        _select = select;
        _post = post;
        _owner = owner;
        _owned = tabs.Where(tab => routeOf(tab) is not null && contentOf(tab) is null).ToList();
    }

    /// <summary>Follows the router, then shows the route of the tab that is selected now.</summary>
    public void Start()
    {
        _router.History.Changed += OnHistoryChanged;
        _post(() =>
        {
            MoveHost();
            GoSelected();
        });
    }

    /// <summary>The user chose another tab: moves the host to it and routes.</summary>
    public void OnSelectionChanged()
    {
        MoveHost();
        GoSelected();
    }

    /// <summary>Stops following the router and takes the host out of its tab.</summary>
    public void Dispose()
    {
        _router.History.Changed -= OnHistoryChanged;
        foreach (var tab in _owned.Where(tab => ReferenceEquals(_contentOf(tab), _host)))
            _setContent(tab, null);
    }

    private void MoveHost()
    {
        var selected = _selected();
        foreach (var tab in _owned.Where(tab => ReferenceEquals(_contentOf(tab), _host) && !ReferenceEquals(tab, selected)))
            _setContent(tab, null);

        if (selected is not null && _owned.Contains(selected) && _contentOf(selected) is null)
            _setContent(selected, _host);
    }

    private void GoSelected()
    {
        if (_selected() is { } selected && _owned.Contains(selected) && _routeOf(selected) is { } route)
            _ = RouteTarget.GoAsync(_provider, _router, route, _owner);
    }

    // The router holds its gate while it signals: the tab is selected after, and does not route again (the route is the current one).
    private void OnHistoryChanged(object? sender, EventArgs e) => _post(() =>
    {
        var tab = _owned.FirstOrDefault(tab => RouteTarget.IsCurrent(_router, _routeOf(tab)));
        if (tab is not null && !ReferenceEquals(tab, _selected()))
            _select(tab);
    });
}

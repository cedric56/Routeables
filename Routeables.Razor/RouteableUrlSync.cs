using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Routing;

namespace Routeables.Razor;

/// <summary>
/// Keeps the browser address and the router in step, for the routes registered with an explicit route
/// (<c>"/customers/{id:int}"</c>, <c>[Routeable("about")]</c>; the leading <c>/</c> is optional and added to the address):
/// <list type="bullet">
/// <item>a route change of the router (push, back, forward...) updates the address: a push adds a browser history entry,
/// the other moves replace it;</item>
/// <item>an address change (link, typed URL, browser back/forward) routes the router: back and forward re-use the
/// router's own history when the address is the previous or next entry, anything else is looked up in the
/// <see cref="RouteableTable"/>;</item>
/// <item>if the router refuses the route change (guard), the address goes back to the current entry.</item>
/// </list>
/// A page registered without a route (the default keys) has no address of its own: the address goes back to <c>/</c>. Started by <c>MainLayout</c>; call <see cref="StartAsync"/> yourself if you use another layout.
/// </summary>
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public sealed class RouteableUrlSync(NavigationManager navigation, IRouter router, IServiceProvider provider) : IDisposable
{
    private bool _started;

    /// <summary>
    /// Starts following the router and the address, then applies the current address.
    /// </summary>
    /// <returns><see langword="true"/> when the current address matched a route and was routed to;
    /// <see langword="false"/> when it matches nothing (the caller then shows its default page).</returns>
    public async Task<bool> StartAsync()
    {
        if (!_started)
        {
            _started = true;
            router.Routed += OnRouted;
            navigation.LocationChanged += OnLocationChanged;
        }
        return await ApplyAsync(Relative(navigation.Uri));
    }

    private void OnRouted(object? sender, RouteableRoutedEventArgs e)
    {
        var address = AddressOf(e.To);
        if (!e.Succeeded || Same(Relative(navigation.Uri), address))
            return;

        Show(address!, replace: e.Kind != RouteableKind.Push || e.From is null);
    }

    private async void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        try { await ApplyAsync(Relative(e.Location)); }
        catch (Exception ex)
        {
            RouteableErrors.Report<RouteableUrlSync>(provider, ex);
            // The address must not keep showing a page that is not displayed.
            if (AddressOf(router.History.Current) is { } current)
                Show(current, replace: true);
        }
    }

    private async Task<bool> ApplyAsync(string path)
    {
        var history = router.History;
        if (Same(AddressOf(history.Current), path))
            return true;

        RouteableResult result;
        if (history.CanGoBack && Same(AddressOf(history.Entries[history.CurrentIndex - 1]), path))
        {
            result = await router.GoBackAsync();
            Moved(result);
        }
        else if (history.CanGoForward && Same(AddressOf(history.Entries[history.CurrentIndex + 1]), path))
        {
            result = await router.ForwardAsync();
            Moved(result);
        }
        else if (provider.GetService<IMainViewModel>() is { } mainViewModel
                 && mainViewModel.Routeables.FirstOrDefault(r => Same(r.Route, path)) is { } item)
        {
            mainViewModel.Routeable = item;       // keeps the layout's selection (menu highlight) in step; it navigates the router
            return true;
        }
        else if (RouteParser.BestMatch(provider.GetService<RouteableTable>()?.Entries ?? [], path).Template is { } template)
        {
            try
            {
                result = await router.GoToAsync(path, RouteableArgs.Create(), template, CancellationToken.None);
            }
            catch (Exception ex)    // a typed or refreshed address the page cannot be built for must not take the app down
            {
                RouteableErrors.Report<RouteableUrlSync>(provider, ex, path);
                result = RouteableResult.Fail(ex);
            }
            // The page was not chosen in the menu: make the layout display it.
            if (result.Success && provider.GetService<IMainViewModel>() is { } shown)
            {
                shown.CurrentView = router.CurrentView;   // setting it raises CurrentViewChanged
            }
        }
        else
        {
            return false;
        }

        // Refused or failed: the address must not keep showing a page that is not displayed.
        if (!result.Success && AddressOf(history.Current) is { } current)
            Show(current, replace: true);
        return true;
    }

    // The browser's Back and Forward moved the router, not the menu: it shows the page that is now current and selects its entry.
    private void Moved(RouteableResult result)
    {
        if (result.Success && provider.GetService<IMainViewModel>() is { } main)
            main.OnRouterMoved(router.CurrentViewModel);
    }

    private void Show(string route, bool replace)
    {
        try { navigation.NavigateTo(route, replace: replace); }
        catch (Exception ex)    // for example while the page is only being prerendered
        {
            RouteableErrors.Report<RouteableUrlSync>(provider, ex, route);
        }
    }

    private string Relative(string uri)
    {
        var path = "/" + navigation.ToBaseRelativePath(uri);
        var hash = path.IndexOf('#');
        return hash < 0 ? path : path[..hash];
    }

    // The address of an entry: its route when the route is registered, "/" for a page that has no route of its own.
    private string? AddressOf(IHistoryEntry? entry)
        => entry is null ? null
           : entry.Args.Route is { } route && IsKnown(route) ? Url(route) : "/";

    // "about" and "/about" are the same address.
    // Exactly one leading slash: "//host" is a protocol-relative link (the browser leaves the application for host), and browsers
    // read "/\host" the same way. A route can carry user input (a returnUrl, a search), so the address stays inside the application.
    private static string Url(string route) => "/" + route.TrimStart('/', '\\');

    private bool IsKnown(string route)
        => provider.GetService<RouteableTable>()?.Entries.Any(e => RouteParser.TryMatch(e.Template, route)) == true;

    private static bool Same(string? a, string? b)
        => a is not null && b is not null
           && string.Equals(Url(a).TrimEnd('/'), Url(b).TrimEnd('/'), StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_started) return;
        _started = false;
        router.Routed -= OnRouted;
        navigation.LocationChanged -= OnLocationChanged;
    }
}
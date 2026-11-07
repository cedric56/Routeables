using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Routeables.Abstraction;
using Routeables.Routing;

namespace Routeables.Blazor;

/// <summary>
/// Keeps the browser address and the router in step, for the routes registered with an explicit route
/// (<c>"/customers/{id:int}"</c>, <c>[Routeable("about")]</c>; the leading <c>/</c> is optional and added to the address):
/// <list type="bullet">
/// <item>a route change of the router (push, back, forward...) updates the address: a push adds a browser history entry. A back, a
/// forward or a pop the router makes itself (<c>GoBackAsync</c> or <c>PopAsync</c> from a view model) is the same move in the history of the browser (<c>history.go</c>),
/// as long as the browser holds the entry it goes to; the other moves replace the address;</item>
/// <item>an address change (link, typed URL, browser back/forward) routes the router: back and forward re-use the
/// router's own history when the address is the previous or next entry, anything else is looked up in the
/// <see cref="RouteableTable"/>;</item>
/// <item>if the router refuses the route change (guard), the address stays on the current entry: the router is asked
/// <em>before</em> the address changes (a location changing handler of the <see cref="NavigationManager"/>), so a refused Back of
/// the browser is cancelled by Blazor and the browser history is left as it was. A <see cref="NavigationManager"/> without that
/// support (some test doubles) is followed after the address changed, and the address is then put back.</item>
/// </list>
/// A page registered without a route (the default keys) has no address of its own: the address goes back to <c>/</c>. Started by <c>MainLayout</c>; call <see cref="StartAsync"/> yourself if you use another layout.
/// </summary>
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public sealed class RouteableUrlSync(NavigationManager navigation, IRouter router, IServiceProvider provider) : IDisposable
{
    private const string HistoryModule = "./_content/Routeables.Blazor/routeables-history.js";

    private bool _started;
    private int _back;             // the entries of the browser history behind the current one that this application pushed
    private int _forward;          // the ones ahead of it, left by a Back
    private Task<IJSObjectReference>? _history;
    private IDisposable? _changing;
    private string? _pending;      // the address being routed to by the location changing handler

    private enum Outcome { Unmatched, Done, Refused }

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
            try { _changing = navigation.RegisterLocationChangingHandler(OnLocationChanging); }
            catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException)
            {
                // No support for it (a test double, prerendering): the address is followed once it has changed.
            }
        }
        return await ApplyAsync(Relative(navigation.Uri)) != Outcome.Unmatched;
    }

    /// <summary>
    /// Routes to the current address while the page is being prerendered, so that the HTML sent to the browser already holds the page.
    /// It follows nothing: there is no circuit yet, and the circuit that takes over starts again with <see cref="StartAsync"/>.
    /// </summary>
    /// <returns><see langword="true"/> when the address matched a route and the router now shows its page.</returns>
    public async Task<bool> PrerenderAsync()
    {
        var path = Relative(navigation.Uri);
        if (RouteParser.BestMatch(provider.GetService<RouteableTable>()?.Entries ?? [], path).Template is not { } template)
            return false;

        RouteableResult result;
        try
        {
            result = await router.GoToAsync(path, RouteableArgs.Create(), template, CancellationToken.None);
        }
        catch (Exception ex)
        {
            RouteableErrors.Report<RouteableUrlSync>(provider, ex, path);
            return false;
        }

        if (result.Success && router.CurrentViewModel is { } current && provider.GetService<IMainViewModel>() is { } main)
        {
            main.OnRouterMoved(current);
        }
        return result.Success;
    }

    private void OnRouted(object? sender, RouteableRoutedEventArgs e)
    {
        if (!e.Succeeded)
            return;

        var address = AddressOf(e.To);
        if (Same(_pending, address))
        {
            Track(e.Kind);      // the browser made this move itself (a link, its Back or Forward button)
            return;
        }

        if (Same(Relative(navigation.Uri), address))
            return;

        // The router goes back or forward by itself (a Pop goes back too: the page it leaves is only forgotten by the router): the browser
        // does the same move when it holds that entry.
        if ((e.Kind is RouteableKind.Back or RouteableKind.Pop && _back > 0 || e.Kind == RouteableKind.Forward && _forward > 0)
            && provider.GetService<IJSRuntime>() is { } js)
        {
            var delta = e.Kind == RouteableKind.Forward ? 1 : -1;
            Track(e.Kind);
            _ = GoInBrowserAsync(js, delta, address!, replace: true);
            return;
        }

        var push = e.Kind == RouteableKind.Push && e.From is not null;
        if (push)
            Track(RouteableKind.Push);
        else if (e.Kind is RouteableKind.Pop or RouteableKind.Root or RouteableKind.MoveTo)
            _back = _forward = 0;     // the router history no longer follows the one of the browser: replace until it does again

        Show(address!, replace: !push);
    }

    private void Track(RouteableKind kind)
    {
        switch (kind)
        {
            case RouteableKind.Push:
                _back++;
                _forward = 0;
                break;
            case RouteableKind.Back:
                if (_back > 0) { _back--; _forward++; }
                break;
            case RouteableKind.Forward:
                if (_forward > 0) { _forward--; _back++; }
                break;
            case RouteableKind.Pop:
                // The router forgets the page it leaves: the browser keeps it ahead, and reaches it again as a new page if asked.
                if (_back > 0) _back--;
                _forward = 0;
                break;
            default:
                _back = _forward = 0;
                break;
        }
    }

    // history.go(delta); when the browser cannot be reached (prerendering, no script), the address is replaced instead.
    private async Task GoInBrowserAsync(IJSRuntime js, int delta, string address, bool replace)
    {
        try
        {
            _history ??= js.InvokeAsync<IJSObjectReference>("import", HistoryModule).AsTask();
            var module = await _history;
            await module.InvokeVoidAsync("go", delta);
        }
        catch (Exception ex) when (ex is InvalidOperationException or JSException or JSDisconnectedException or TaskCanceledException)
        {
            _history = null;
            _back = _forward = 0;
            Show(address, replace);
        }
    }

    // Runs before the address changes, for a link, a typed address and the Back and Forward buttons of the browser:
    // a route the router refuses cancels the navigation, and Blazor puts the browser back where it was.
    private async ValueTask OnLocationChanging(LocationChangingContext context)
    {
        if (!context.TargetLocation.StartsWith(navigation.BaseUri, StringComparison.OrdinalIgnoreCase))
            return;     // another site

        var path = Relative(context.TargetLocation);
        if (Same(AddressOf(router.History.Current), path))
            return;     // the address of the page that is shown: the router moved first

        _pending = path;
        try
        {
            if (await ApplyAsync(path, restore: false) == Outcome.Refused)
                context.PreventNavigation();
        }
        catch (Exception ex)
        {
            RouteableErrors.Report<RouteableUrlSync>(provider, ex, path);
            context.PreventNavigation();
        }
        finally
        {
            _pending = null;
        }
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

    private async Task<Outcome> ApplyAsync(string path, bool restore = true)
    {
        var history = router.History;
        if (Same(AddressOf(history.Current), path))
            return Outcome.Done;

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
                 && mainViewModel.Entries.FirstOrDefault(r => Same(r.Route, path)) is { } item)
        {
            mainViewModel.SelectedEntry = item;       // keeps the layout's selection (menu highlight) in step; it navigates the router
            return Outcome.Done;
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
            // The page was not chosen in the menu: the menu follows it, and the layout displays it.
            if (result.Success && provider.GetService<IMainViewModel>() is { } shown)
                shown.OnRouterMoved(router.CurrentViewModel);
        }
        else
        {
            return Outcome.Unmatched;
        }

        if (result.Success)
            return Outcome.Done;

        // Refused or failed: the address must not keep showing a page that is not displayed.
        if (restore && AddressOf(history.Current) is { } current)
            Show(current, replace: true);
        return Outcome.Refused;
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

    // The browser reports an address escaped ("/dogs/Afghan%20Hound") and a route is kept as it was asked ("/dogs/Afghan Hound"): both are
    // the same address, so they are compared unescaped. Without it the two never match and the address is shown again and again.
    private static bool Same(string? a, string? b)
        => a is not null && b is not null
           && string.Equals(Normalize(a), Normalize(b), StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string address)
    {
        var path = Url(address).TrimEnd('/');
        try { return Uri.UnescapeDataString(path); }
        catch (UriFormatException) { return path; }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_started) return;
        _started = false;
        _changing?.Dispose();
        _changing = null;
        router.Routed -= OnRouted;
        navigation.LocationChanged -= OnLocationChanged;
    }
}
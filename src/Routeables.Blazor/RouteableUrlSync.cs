using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Errors;
using Routeables.Abstraction.History;
using Routeables.Abstraction.Lifecycle;
using Routeables.Abstraction.Registrations;
using Routeables.Abstraction.Routing;
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
public sealed class RouteableUrlSync(NavigationManager navigation, IRouter router, IServiceProvider provider) : IDisposable, IAsyncDisposable
{
    private const string HistoryModule = "./_content/Routeables.Blazor/routeables-history.js";
    private const string UnloadModule = "./_content/Routeables.Blazor/routeables-unload.js";

    private bool _started;
    private volatile bool _ended;      // Dispose ran: no script is imported from now on, and one that arrives late is let go of by DisposeAsync
    private int _back;             // the entries of the browser history behind the current one that this application pushed
    private int _forward;          // the ones ahead of it, left by a Back
    private Task<IJSObjectReference>? _history;
    private Task<IJSObjectReference>? _unload;
    private IRouteableUnsavedChanges? _watched;   // the page whose unsaved changes the browser is told about
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
            _ended = false;
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

        if (result.Success && router.CurrentViewModel is { } current && provider.GetService<IRouteableMenuViewModel>() is { } main)
        {
            main.OnRouterMoved(current);
        }
        return result.Success;
    }

    private void OnRouted(object? sender, RouteableRoutedEventArgs e)
    {
        if (!e.Succeeded)
            return;

        WatchUnsavedChanges();

        var address = AddressOf(e.To);
        if (SameAddress(_pending, address))
        {
            Track(e.Kind);      // the browser made this move itself (a link, its Back or Forward button)
            return;
        }

        if (SameAddress(Relative(navigation.Uri), address))
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

    // The browser asks before the tab closes while the page shown says it has unsaved changes (IRouteableUnsavedChanges).
    private void WatchUnsavedChanges()
    {
        var page = router.CurrentViewModel as IRouteableUnsavedChanges ?? router.CurrentView as IRouteableUnsavedChanges;
        if (ReferenceEquals(page, _watched))
            return;

        if (_watched is not null)
            _watched.UnsavedChangesChanged -= OnUnsavedChangesChanged;
        _watched = page;
        if (page is not null)
            page.UnsavedChangesChanged += OnUnsavedChangesChanged;
        _ = TellBrowserAsync();
    }

    private void OnUnsavedChangesChanged(object? sender, EventArgs e) => _ = TellBrowserAsync();

    private async Task TellBrowserAsync()
    {
        var warn = _watched?.HasUnsavedChanges == true;
        if (_ended)
            return;
        if (!warn && _unload is null)
            return;                     // nothing was ever asked: no script to load for "no warning"
        if (provider.GetService<IJSRuntime>() is not { } js)
            return;
        try
        {
            _unload ??= js.InvokeAsync<IJSObjectReference>("import", UnloadModule).AsTask();
            var module = await _unload;
            if (_ended)
                return;                 // the circuit ended while the script was loading: DisposeAsync lets go of it
            await module.InvokeVoidAsync("setWarn", warn);
        }
        catch (Exception ex) when (ex is InvalidOperationException or JSException or JSDisconnectedException or TaskCanceledException)
        {
            _unload = null;             // prerendering or no script: there is no tab to warn about
        }
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
        if (SameAddress(AddressOf(router.History.Current), path))
            return;     // the address of the page that is shown: the router moved first

        _pending = path;
        try
        {
            if (await ApplyAsync(path, restore: false, context.CancellationToken) == Outcome.Refused)
                context.PreventNavigation();
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            // Blazor dropped this navigation for a newer one: nothing to report, and nothing to prevent.
        }
        catch (Exception ex)
        {
            RouteableErrors.Report<RouteableUrlSync>(provider, ex, path);
            context.PreventNavigation();
        }
        finally
        {
            // Another navigation may have replaced the address in the meantime: only the one that set it clears it.
            if (ReferenceEquals(_pending, path))
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

    private async Task<Outcome> ApplyAsync(string path, bool restore = true, CancellationToken cancellationToken = default)
    {
        var history = router.History;
        if (SameAddress(AddressOf(history.Current), path))
            return Outcome.Done;

        RouteableResult result;
        if (history.CanGoBack && SameAddress(AddressOf(history.Entries[history.CurrentIndex - 1]), path))
        {
            result = await router.GoBackAsync(cancellationToken);
            Moved(result);
        }
        else if (history.CanGoForward && SameAddress(AddressOf(history.Entries[history.CurrentIndex + 1]), path))
        {
            result = await router.ForwardAsync(cancellationToken);
            Moved(result);
        }
        else if (provider.GetService<IRouteableMenuViewModel>() is { } menuViewModel
                 && menuViewModel.Entries.FirstOrDefault(r => SameAddress(r.Route, path)) is { } item)
        {
            // Keeps the layout's selection (menu highlight) in step; it navigates the router. The guards answer before the address is accepted.
            if (menuViewModel is Routeables.Core.RouteableMenuViewModelBase selectable)
            {
                if (await selectable.SelectEntryAsync(item))
                    return Outcome.Done;
                if (restore && AddressOf(history.Current) is { } shownNow)
                    Show(shownNow, replace: true);
                return Outcome.Refused;
            }

            menuViewModel.SelectedEntry = item;
            return Outcome.Done;
        }
        else if (RouteParser.BestMatch(provider.GetService<RouteableTable>()?.Entries ?? [], path).Template is { } template)
        {
            try
            {
                result = await router.GoToAsync(path, RouteableArgs.Create(), template, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return Outcome.Refused;     // the browser asked for another address meanwhile: this one is no longer wanted
            }
            catch (Exception ex)    // a typed or refreshed address the page cannot be built for must not take the app down
            {
                RouteableErrors.Report<RouteableUrlSync>(provider, ex, path);
                result = RouteableResult.Fail(ex);
            }
            // The page was not chosen in the menu: the menu follows it, and the layout displays it.
            if (result.Success && provider.GetService<IRouteableMenuViewModel>() is { } shown)
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
        if (result.Success && provider.GetService<IRouteableMenuViewModel>() is { } main)
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
    // The path ignores the case and a closing slash; the query is data: the names ignore the case, the values do not, and a slash that ends a
    // value is part of it ("/search?q=ABC" is not "/search?q=abc", "?path=a/" is not "?path=a").
    internal static bool Same(string? a, string? b, IEnumerable<(string Template, Type? ViewModelType)>? routes = null)
    {
        if (a is null || b is null)
            return false;
        var (pathA, queryA) = SplitQuery(Url(a));
        var (pathB, queryB) = SplitQuery(Url(b));
        return SamePath(Unescape(pathA.TrimEnd('/')), Unescape(pathB.TrimEnd('/')), routes)
               && SameQuery(queryA, queryB);
    }

    private bool SameAddress(string? a, string? b) => Same(a, b, provider.GetService<RouteableTable>()?.Entries);

    // The words of a route ignore the case ("/Orders" is "/orders"), the values of its placeholders do not: "/items/aB" and "/items/Ab" are two
    // pages when the identifier is a token or a slug. A path whose route is not known has no placeholder to protect.
    private static bool SamePath(string a, string b, IEnumerable<(string Template, Type? ViewModelType)>? routes)
    {
        if (string.Equals(a, b, StringComparison.Ordinal))
            return true;
        if (!string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
            return false;
        if (routes is null || RouteParser.BestMatch(routes, a).Template is not { } template)
            return true;

        var templateSegments = template.Split('?')[0].Split('/', StringSplitOptions.RemoveEmptyEntries);
        var segmentsA = a.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var segmentsB = b.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < segmentsA.Length && i < segmentsB.Length; i++)
        {
            if (string.Equals(segmentsA[i], segmentsB[i], StringComparison.Ordinal))
                continue;
            if (i >= templateSegments.Length || templateSegments[i].Contains('{'))
                return false;   // the case of a value changed: not the same page
        }

        return true;
    }

    private static (string Path, string? Query) SplitQuery(string address)
    {
        var at = address.IndexOf('?');
        return at < 0 ? (address, null) : (address[..at], address[(at + 1)..]);
    }

    private static bool SameQuery(string? a, string? b)
    {
        // "?x=1&y=2" is "?y=2&x=1": the pairs are put in order of name (the sort is stable, so the values of a repeated name keep their order).
        var pairsA = (a ?? string.Empty).Split('&', StringSplitOptions.RemoveEmptyEntries).OrderBy(p => SplitPair(p).Name, StringComparer.OrdinalIgnoreCase).ToArray();
        var pairsB = (b ?? string.Empty).Split('&', StringSplitOptions.RemoveEmptyEntries).OrderBy(p => SplitPair(p).Name, StringComparer.OrdinalIgnoreCase).ToArray();
        if (pairsA.Length != pairsB.Length)
            return false;
        for (var i = 0; i < pairsA.Length; i++)
        {
            var (nameA, valueA) = SplitPair(pairsA[i]);
            var (nameB, valueB) = SplitPair(pairsB[i]);
            if (!string.Equals(Unescape(nameA), Unescape(nameB), StringComparison.OrdinalIgnoreCase)
                || !string.Equals(Unescape(valueA), Unescape(valueB), StringComparison.Ordinal))
                return false;
        }
        return true;
    }

    private static (string Name, string Value) SplitPair(string pair)
    {
        var at = pair.IndexOf('=');
        return at < 0 ? (pair, string.Empty) : (pair[..at], pair[(at + 1)..]);
    }

    private static string Unescape(string text)
    {
        // An escaped slash is not a separator: "/files/a%2Fb" is one segment and "/files/a/b" is two, so the first must stay escaped.
        try { return Uri.UnescapeDataString(text.Replace("%2F", "%252F", StringComparison.OrdinalIgnoreCase)); }
        catch (UriFormatException) { return text; }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _ended = true;
        if (!_started) return;
        _started = false;
        _changing?.Dispose();
        _changing = null;
        router.Routed -= OnRouted;
        navigation.LocationChanged -= OnLocationChanged;
        if (_watched is not null)
            _watched.UnsavedChangesChanged -= OnUnsavedChangesChanged;
        _watched = null;
    }

    /// <summary>Stops following, and lets go of the script module that moves the history of the browser, which a scope that ends asynchronously owns.</summary>
    public async ValueTask DisposeAsync()
    {
        Dispose();
        foreach (var module in new[] { _history, _unload })
        {
            if (module is not null)
            {
                try
                {
                    // A script still loading is waited for: it would otherwise install its listener with nobody left to remove it.
                    var loaded = await module;
                    if (ReferenceEquals(module, _unload)) await loaded.InvokeVoidAsync("dispose");   // the listener of the tab must not outlive the circuit
                    await loaded.DisposeAsync();
                }
                catch (Exception ex) when (ex is JSDisconnectedException or InvalidOperationException or JSException or TaskCanceledException) { }   // the circuit is already gone
            }
        }
        _history = null;
        _unload = null;
    }
}
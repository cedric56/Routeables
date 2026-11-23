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
    private volatile bool _ended; // Dispose ran: no script is imported from now on, and one that arrives late is released by DisposeAsync
    private readonly BrowserHistoryDepth _depth = new(); // the browser history entries behind and ahead of the current one
    private Task<IJSObjectReference>? _history;
    private Task<IJSObjectReference>? _unload;
    private IRouteableUnsavedChanges? _watched; // the page whose unsaved changes the browser is told about
    private IDisposable? _changing;
    private string? _pending; // the address being routed to by the location-changing handler
    // The entry of the browser that the browser pushed itself (a link, a typed address) for the router entry it now shows: once the address has
    // changed, the browser entry is given the state of that router entry, so that its Back and Forward can name it.
    private (string Address, IHistoryEntry Entry)? _toStamp;

    // Each router entry has a state that its browser entry keeps (history.state), which tells the Back and the Forward of the browser apart.
    private readonly BrowserEntryStates _states = new();

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
                // No support for it (a test double, prerendering): follow the address once it has changed.
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
            _depth.Track(e.Kind); // the browser made this move itself (a link, its Back or Forward button)
            // A link or a typed address: the browser pushes its entry once the handlers have answered, without a state. It is given one then.
            if (e.Kind == RouteableKind.Push)
                _toStamp = (address!, e.To);
            return;
        }

        // The address is already shown: nothing to write, unless the application pushed the page again (a second "/home" after "/home"). The router
        // then has one more entry, and the browser needs one too, or every Back and Forward after it would land one entry off.
        if (SameAddress(Relative(navigation.Uri), address) && !(e.Kind == RouteableKind.Push && e.From is not null))
            return;

        // The router goes back or forward by itself (a Pop goes back too: the router only forgets the page it leaves). The browser makes the same
        // move when it holds that entry.
        if (_depth.CanFollow(e.Kind) && provider.GetService<IJSRuntime>() is { } js)
        {
            var delta = e.Kind == RouteableKind.Forward ? 1 : -1;
            _depth.Track(e.Kind);
            _ = GoInBrowserAsync(js, delta, address!, replace: true, _states.StateOf(e.To));
            return;
        }

        var push = e.Kind == RouteableKind.Push && e.From is not null;
        if (push)
            _depth.Track(RouteableKind.Push);
        else if (e.Kind is RouteableKind.Pop or RouteableKind.Root or RouteableKind.MoveTo)
            _depth.Reset(); // the router history no longer follows the browser's: replace until it does again

        Show(address!, replace: !push, _states.StateOf(e.To));
    }

    // The state of the entry the router shows: an address put back must keep it, or that browser entry could no longer be named.
    private string? StateOfCurrent() => router.History.Current is { } current ? _states.StateOf(current) : null;

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
            return; // nothing was ever asked: no script to load for "no warning"
        if (provider.GetService<IJSRuntime>() is not { } js)
            return;
        try
        {
            _unload ??= js.InvokeAsync<IJSObjectReference>("import", UnloadModule).AsTask();
            var module = await _unload;
            if (_ended)
                return; // the circuit ended while the script was loading: DisposeAsync lets go of it
            await module.InvokeVoidAsync("setWarn", warn);
        }
        catch (Exception ex) when (ex is InvalidOperationException or JSException or JSDisconnectedException or TaskCanceledException)
        {
            _unload = null; // prerendering or no script: there is no tab to warn about
        }
    }

    // history.go(delta); when the browser cannot be reached (prerendering, no script), replace the address instead.
    private async Task GoInBrowserAsync(IJSRuntime js, int delta, string address, bool replace, string state)
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
            _depth.Reset();
            Show(address, replace, state);
        }
    }

    // Runs before the address changes (a link, a typed address, the browser's Back and Forward). A route the router refuses cancels the
    // navigation, and Blazor puts the browser back where it was.
    private async ValueTask OnLocationChanging(LocationChangingContext context)
    {
        if (!IsInsideBase(context.TargetLocation))
            return; // another site

        var path = Relative(context.TargetLocation);
        // The address of the page shown: the router moved first (it is what the router itself writes, and it must not start a navigation).
        // A link the user clicked is different: from a page that has no route, "/" is the home, not the page that is shown.
        if (SameAddress(context.IsNavigationIntercepted ? OwnAddressOf(router.History.Current) : AddressOf(router.History.Current), path))
            return;

        _pending = path;
        try
        {
            if (await ApplyAsync(path, restore: false, context.CancellationToken, fromLink: context.IsNavigationIntercepted, state: context.HistoryEntryState) == Outcome.Refused)
                context.PreventNavigation();
        }
        catch (OperationCanceledException)
        {
            // Blazor dropped this navigation for a newer one, or the router replaced it (CancelCurrent): nothing to report and nothing to prevent.
        }
        catch (Exception ex)
        {
            RouteableErrors.Report<RouteableUrlSync>(provider, ex, path);
            context.PreventNavigation();
        }
        finally
        {
            // Another navigation may have replaced the address meanwhile: only the one that set it clears it.
            if (ReferenceEquals(_pending, path))
                _pending = null;
        }
    }

    private async void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        try
        {
            var path = Relative(e.Location);
            // The browser pushed the entry of a link or a typed address that the router now shows: give it the state of the router entry.
            if (_toStamp is { } stamp)
            {
                _toStamp = null;
                if (e.HistoryEntryState is null && SameAddress(stamp.Address, path) && ReferenceEquals(router.History.Current, stamp.Entry))
                {
                    Show(stamp.Address, replace: true, _states.StateOf(stamp.Entry));
                    return;
                }
            }
            // The browser moved (a NavigationManager without a location changing handler is only followed here): the route change this starts is
            // the browser's own, as it is in OnLocationChanging, and must not write an entry of its own.
            _pending = path;
            try { await ApplyAsync(path, state: e.HistoryEntryState); }
            finally
            {
                if (ReferenceEquals(_pending, path))
                    _pending = null;
            }
        }
        catch (OperationCanceledException)
        {
            // A newer navigation replaced this one (RouterConcurrency.CancelCurrent): it decides what the address shows.
        }
        catch (Exception ex)
        {
            RouteableErrors.Report<RouteableUrlSync>(provider, ex);
            // The address must not keep showing a page that is not displayed. This is an async void handler: nothing may leave it.
            try
            {
                if (AddressOf(router.History.Current) is { } current)
                    Show(current, replace: true, StateOfCurrent());
            }
            catch (Exception again)
            {
                RouteableErrors.Report<RouteableUrlSync>(provider, again);
            }
        }
    }

    // fromLink: a link the user clicked. The browser pushed a new entry for it, even when it leads to the page before or after the current one,
    // so the router pushes too: going back would otherwise leave the router and the browser with different histories.
    private async Task<Outcome> ApplyAsync(string path, bool restore = true, CancellationToken cancellationToken = default, bool fromLink = false, string? state = null)
    {
        var history = router.History;
        if (SameAddress(fromLink ? OwnAddressOf(history.Current) : AddressOf(history.Current), path))
            return Outcome.Done;

        RouteableResult result;
        string? template = null;

        // The neighbours are read once, from one copy of the entries: a route change that runs meanwhile (the restore of the history) must not
        // leave an index that points outside the copy.
        IHistoryEntry? before = null, after = null;
        var named = -1; // the entry the state of the browser entry names, when it names one
        var index = -1;
        if (!fromLink)
        {
            (var entries, index) = history.Capture();
            before = index > 0 && index - 1 < entries.Count ? entries[index - 1] : null;
            after = index >= 0 && index + 1 < entries.Count ? entries[index + 1] : null;
            named = _states.IndexOf(state, entries);
        }

        // The state names the entry: [home, detail, home] has the same address before and after the detail, the state tells them apart. Without
        // one (an entry this application did not write, or a router that forgot it), the address decides.
        var back = named >= 0 ? named == index - 1 : before is not null && SameAddress(AddressOf(before), path);
        var forward = named >= 0 ? named == index + 1 : after is not null && SameAddress(AddressOf(after), path);

        if (back)
        {
            result = await router.GoBackAsync(cancellationToken);
            Moved(result);
        }
        else if (forward)
        {
            result = await router.ForwardAsync(cancellationToken);
            Moved(result);
        }
        else if (named >= 0 && named != index)
        {
            // A jump of several entries (a long press on Back, history.go(-2)): the state names the entry, so the router goes there too
            // instead of adding a page.
            result = await router.MoveToAsync(named, cancellationToken);
            Moved(result);
        }
        else if (provider.GetService<IRouteableMenuViewModel>() is { } menuViewModel
                 && menuViewModel.Entries.FirstOrDefault(r => SameAddress(r.Route, path)) is { } item)
        {
            // Keeps the layout's selection (menu highlight) in step; it navigates the router. The guards answer before the address is accepted (a menu
            // written by hand answers true: see IRouteableMenuViewModel.SelectEntryAsync).
            if (await menuViewModel.SelectEntryAsync(item, cancellationToken))
                return Outcome.Done;
            if (restore && AddressOf(history.Current) is { } shownNow)
                Show(shownNow, replace: true, StateOfCurrent());
            return Outcome.Refused;
        }
        else if ((template = RouteParser.BestMatch(provider.GetService<RouteableTable>()?.Entries ?? [], path).Template) is not null
                 || provider.GetService<RouteableNotFound>() is not null)
        {
            // No template for the address, and a page for such addresses (UseNotFoundRoute): the router shows it, as it does for GoToAsync.
            try
            {
                result = await router.GoToAsync(path, RouteableArgs.Create(), template, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // The browser asked for another address meanwhile, or a newer navigation replaced this one (RouterConcurrency.CancelCurrent), which cancels
                // it with a router token and not the browser's: either way this address is no longer wanted.
                return Outcome.Refused;
            }
            catch (Exception ex) // a typed or refreshed address the page cannot be built for must not take the app down
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
            Show(current, replace: true, StateOfCurrent());
        return Outcome.Refused;
    }

    // The browser's Back and Forward moved the router, not the menu: show the page that is now current and select its entry.
    private void Moved(RouteableResult result)
    {
        if (result.Success && provider.GetService<IRouteableMenuViewModel>() is { } main)
            main.OnRouterMoved(router.CurrentViewModel);
    }

    private void Show(string route, bool replace, string? state = null)
    {
        try { navigation.NavigateTo(BaseRelative(route), new NavigationOptions { ReplaceHistoryEntry = replace, HistoryEntryState = state }); }
        catch (Exception ex) // for example while the page is only being prerendered
        {
            RouteableErrors.Report<RouteableUrlSync>(provider, ex, route);
        }
    }

    // The base address ends with a slash ("https://site/app/"); the application's address without it ("https://site/app") is the same page, and
    // Blazor reads it so (ToBaseRelativePath gives "").
    private bool IsInsideBase(string location)
    {
        var baseUri = navigation.BaseUri;
        // The case counts, as it does in ToBaseRelativePath (which throws for "/App/orders" under "/app/"): this test must say yes only to what that call accepts.
        return location.StartsWith(baseUri, StringComparison.Ordinal)
               || (location.Length == baseUri.Length - 1 && baseUri.StartsWith(location, StringComparison.Ordinal));
    }

    private string Relative(string uri)
    {
        return RouteAddress.WithoutFragment("/" + navigation.ToBaseRelativePath(uri));
    }

    // An entry's address: its route when the route is registered, "/" for a page that has no route of its own.
    private string? AddressOf(IHistoryEntry? entry)
        => entry is null ? null
           : entry.Args.Route is { } route && IsKnown(route) ? Url(route) : "/";

    // The address a page has of its own: none for a page without a route. Such a page is shown under "/" (the browser needs one), which is also
    // the address of a home registered on "/": a link to "/" from that page must still go there, not be taken for the page shown.
    private string? OwnAddressOf(IHistoryEntry? entry)
        => entry?.Args.Route is { } route && IsKnown(route) ? Url(route) : null;

    // "about" and "/about" are the same address.
    // Exactly one leading slash: "//host" is a protocol-relative link (the browser leaves the application for host), and browsers read "/\host"
    // the same way. A route can carry user input (a returnUrl, a search), so the address must stay inside the application.
    private static string Url(string route) => RouteAddress.Rooted(route);

    /// <summary>True once the synchronisation listens to the address changes: a click on a plain link is then routed through the router.</summary>
    internal bool IsFollowing => _changing is not null;

    /// <summary>
    /// The address as Blazor wants it written: relative to the base address. "/orders/42" is read from the root of the site, so under
    /// <c>&lt;base href="/app/"&gt;</c> it leaves the application; "./orders/42" stays inside. The "./" also keeps "javascript:x" from being read as a scheme.
    /// </summary>
    internal static string BaseRelative(string route) => "./" + RouteAddress.Rooted(route).TrimStart('/');

    private bool IsKnown(string route)
        => provider.GetService<RouteableTable>()?.Entries.Any(e => RouteParser.TryMatch(e.Template, route)) == true;

    // The rules of "the same address" (case, closing slash, escapes, query order) are those of the whole library: see RouteAddress.
    internal static bool Same(string? a, string? b, IEnumerable<(string Template, Type? ViewModelType)>? routes = null)
        => RouteAddress.Same(a, b, routes is null ? null : path => RouteParser.BestMatch(routes, path).Template);

    private bool SameAddress(string? a, string? b) => Same(a, b, provider.GetService<RouteableTable>()?.Entries);

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
                    // Wait for a script that is still loading: it would otherwise install its listener with nobody left to remove it.
                    var loaded = await module;
                    if (ReferenceEquals(module, _unload)) await loaded.InvokeVoidAsync("dispose"); // the tab's listener must not outlive the circuit
                    await loaded.DisposeAsync();
                }
                catch (Exception ex) when (ex is JSDisconnectedException or InvalidOperationException or JSException or TaskCanceledException) { } // the circuit is already gone
            }
        }
        _history = null;
        _unload = null;
    }
}
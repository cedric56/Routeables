using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;

namespace Routeables.Routing;

/// <summary>
/// Provides ViewModel-first route change with history and lifecycle callbacks.
/// Routing operations are serialized per router instance.
/// </summary>
internal class Router(
    RouterHistory history,
    RouterGate gate,
    RouterLifecycle lifecycle,
    IServiceProvider provider) : IRouter, ILockCoordinatorFactory
{
    /// <summary>
    /// Occurs when a routing operation is about to begin.
    /// </summary>
    public event EventHandler<RouteableRoutingEventArgs>? Routing;

    /// <summary>
    /// Occurs after a routing operation has completed (whether successful, failed, or cancelled).
    /// </summary>
    public event EventHandler<RouteableRoutedEventArgs>? Routed;

    /// <summary>
    /// Gets the route change history stack and current entry tracker.
    /// </summary>
    public IRouterHistory History => history;

    /// <summary>
    /// Gets a value indicating whether routing can go back to a previous entry.
    /// </summary>
    public bool CanGoBack => history.CanGoBack;

    /// <summary>
    /// Gets a value indicating whether routing can go forward to a subsequent entry.
    /// </summary>
    public bool CanGoForward => history.CanGoForward;

    /// <summary>
    /// Gets the view model instance of the currently active routing entry.
    /// </summary>
    public object? CurrentViewModel => history.Current?.ViewModel;

    /// <summary>
    /// Gets the view instance of the currently active routing entry.
    /// </summary>
    public object? CurrentView => history.Current?.View;

    // ------------------------------------------------------------------ events

    // Observers must never break, or mask the error of, a route change.
    private void Raise<T>(EventHandler<T>? handlers, object sender, T args) where T : EventArgs
        => RouteableEvents.Raise(provider, "Router", handlers, sender, args);

    // Call only while the gate is held.
    private async Task<RouteableResult> ObserveAsync(
        RouteableKind kind, RouterEntry? from, RouterEntry to,
        CancellationToken ct, Func<Task<RouteableResult>> run)
    {
        Raise(Routing, this, new RouteableRoutingEventArgs(kind, from, to));

        RouteableResult result;
        try
        {
            result = await run();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            Raise(Routed, this, new RouteableRoutedEventArgs(kind, from, to, RouteableOutcome.Cancelled));
            throw;
        }
        catch (Exception ex)
        {
            Raise(Routed, this, new RouteableRoutedEventArgs(kind, from, to, RouteableOutcome.Failed, ex));
            throw;
        }

        var outcome = result.Success ? RouteableOutcome.Success
                    : result.Cancelled ? RouteableOutcome.Cancelled
                    : RouteableOutcome.Failed;
        Raise(Routed, this, new RouteableRoutedEventArgs(kind, from, to, outcome));
        return result;
    }

    // ------------------------------------------------------------- history moves

    // The gate is entered inline in each public method (not through an async helper): its AsyncLocal flag
    // has to be set in the method's own flow so that the callbacks awaited below can see it.
    // Call only while the gate is held.
    private async Task<RouteableResult> MoveAsync(
        RouteableKind kind,
        RouterEntry? target,
        Func<IReadOnlyList<RouterEntry>> commit,
        CancellationToken ct)
    {
        var leaving = history.Current;
        if (leaving is null || target is null || ReferenceEquals(leaving, target))
            return RouteableResult.Cancel();                // nothing to do: no events

        return await ObserveAsync(kind, leaving, target, ct, async () =>
        {
            if (!await lifecycle.CanLeaveAsync(leaving, ct)) return RouteableResult.Cancel();

            await lifecycle.TransitionAsync(leaving, target, ct);  // throws -> history untouched

            var removed = commit();                                // only after the lifecycle succeeded
            await lifecycle.NotifyEnteredAsync(leaving, target, ct);   // before the removed entries are disposed
            await lifecycle.DestroyAsync(removed, history.Snapshot());
            return RouteableResult.Ok();
        });
    }

    /// <inheritdoc />
    public async Task<RouteableResult> GoBackAsync(CancellationToken cancellationToken = default)
    {
        var enter = gate.EnterAsync(cancellationToken);
        try { await enter; } catch { gate.Abort(); throw; }
        try
        {
            return await MoveAsync(RouteableKind.Back, history.PeekBack(),
                () => { history.MoveBack(); return []; }, cancellationToken);
        }
        finally { gate.Exit(); }
    }

    /// <inheritdoc />
    public async Task<RouteableResult> ForwardAsync(CancellationToken cancellationToken = default)
    {
        var enter = gate.EnterAsync(cancellationToken);
        try { await enter; } catch { gate.Abort(); throw; }
        try
        {
            return await MoveAsync(RouteableKind.Forward, history.PeekForward(),
                () => { history.MoveForward(); return []; }, cancellationToken);
        }
        finally { gate.Exit(); }
    }

    /// <inheritdoc />
    public async Task<RouteableResult> GoBackToRootAsync(CancellationToken cancellationToken = default)
    {
        var enter = gate.EnterAsync(cancellationToken);
        try { await enter; } catch { gate.Abort(); throw; }
        try
        {
            return await MoveAsync(RouteableKind.Root, history.Root,
                history.MoveToRoot, cancellationToken);
        }
        finally { gate.Exit(); }
    }

    /// <inheritdoc />
    public async Task<RouteableResult> PopAsync(CancellationToken cancellationToken = default)
    {
        var enter = gate.EnterAsync(cancellationToken);
        try { await enter; } catch { gate.Abort(); throw; }
        try
        {
            return await MoveAsync(RouteableKind.Pop, history.PeekBack(),
                history.Pop, cancellationToken);
        }
        finally { gate.Exit(); }
    }

    /// <inheritdoc />
    public async Task<RouteableResult> MoveToAsync(int index, CancellationToken cancellationToken = default)
    {
        var enter = gate.EnterAsync(cancellationToken);
        try { await enter; } catch { gate.Abort(); throw; }
        try
        {
            return await MoveAsync(RouteableKind.MoveTo, history.At(index),
                () => { history.MoveTo(index); return []; }, cancellationToken);
        }
        finally { gate.Exit(); }
    }

    /// <inheritdoc />
    public async Task EraseHistoryAsync(CancellationToken cancellationToken = default)
    {
        var enter = gate.EnterAsync(cancellationToken);
        try { await enter; } catch { gate.Abort(); throw; }
        try
        {
            var removed = history.Clear();
            await lifecycle.DestroyAsync(removed, history.Snapshot());   // snapshot is empty now
        }
        finally { gate.Exit(); }
    }

    /// <inheritdoc />
    public Task<RouteableResult> GoToRouteAsync(
        string route,
        IRouteableArgs? args = null,
        string? template = null,
        CancellationToken cancellationToken = default,
        params string[] viewKeys)
    {
        ArgumentNullException.ThrowIfNull(route);
        template ??= TemplateOf(route);
        return GoToRouteAsync(route, template, template, template, args, null, null, cancellationToken, viewKeys);
    }

    /// <summary>
    /// The template of a route that was not given one: the one registered for it (an address like <c>/orders/42</c> finds
    /// <c>/orders/{id:int}</c>), else the one of the menu entry with that route, else the route itself (a route without parameter
    /// is its own template).
    /// </summary>
    private string TemplateOf(string route)
    {
        static string Normal(string? value) => (value ?? string.Empty).Split('?')[0].Trim('/');

        var entries = provider.GetService<RouteableTable>()?.Entries ?? [];
        var exact = entries.FirstOrDefault(e => string.Equals(Normal(e.Template), Normal(route), StringComparison.OrdinalIgnoreCase));
        if (exact.Template is not null)
            return exact.Template;

        var matching = entries.FirstOrDefault(e => RouteParser.TryMatch(e.Template, route));
        if (matching.Template is not null)
            return matching.Template;

        var item = provider.GetService<IMainRouteables>()?.Routeables
            .FirstOrDefault(r => string.Equals(Normal(r.Route), Normal(route), StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(r.Template));
        return item?.Template ?? route;
    }

    public async Task<RouteableResult> GoAsync(object view, object viewModel, IRouteableArgs? args = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(viewModel);

        var enter = gate.EnterAsync(cancellationToken);
        try { await enter; } catch { gate.Abort(); throw; }

        try
        {
            IRouteableFor? viewFor = view as IRouteableFor;
            var previousContext = viewFor?.Context;    // inside the try: a throwing getter must not leave the gate held
            args ??= RouteableArgs.Create();

            var previous = history.Current;
            var entering = new RouterEntry(viewModel, view, args);

            return await ObserveAsync(RouteableKind.Push, previous, entering, cancellationToken, async () =>
            {
                try
                {
                    // 1. Can the current page be left?
                    if (previous is not null && !await lifecycle.CanLeaveAsync(previous, cancellationToken))
                        return RouteableResult.Cancel();

                    // 2. Bind view <-> view-model
                    if (viewFor is not null)
                        viewFor.Context = viewModel;

                    // 3. Initialize the target (VM first, then view). Nothing has been torn down yet.
                    await lifecycle.InitializeAsync(entering, cancellationToken);

                    // 4. Leave current + enter target (or just enter on first route change)
                    if (previous is not null)
                        await lifecycle.TransitionAsync(previous, entering, cancellationToken);
                    else
                        await lifecycle.EnterAsync(entering, cancellationToken);

                    // 5. Commit, then destroy what the push cut off (forward entries, evicted oldest)
                    var removed = history.Push(entering);
                    await lifecycle.NotifyEnteredAsync(previous, entering, cancellationToken);
                    await lifecycle.DestroyAsync(removed, history.Snapshot());
                    return RouteableResult.Ok();
                }
                catch
                {
                    if (viewFor is not null)
                        viewFor.Context = previousContext;    // restored before Navigated(Failed) is raised
                    throw;
                }
            });
        }
        finally { gate.Exit(); }
    }

    /// <summary>
    /// Internal implementation for route-based routing, resolving view models and views from the service provider.
    /// </summary>
    internal async Task<RouteableResult> GoToRouteAsync(
        string route,
        string template,
        string viewkey,
        string viewmodelkey,
        IRouteableArgs? args = null,
        object? viewModel = null,
        object? view = null,
        CancellationToken cancellationToken = default,
        params string[] viewKeys)
    {
        // One route change, one arguments instance. The caller's (RouteableEntry.Arguments is reused on every click) is only a
        // model: TryMatch appends the route values to its bags and Route is written, so two history entries sharing it
        // would see each other's values (doubled constructor arguments, a duplicate key, the wrong address on Back).
        args = Snapshot(args);
        // The view model this route change builds receives this router if it asks for an IRouter (a region is not the default router).
        args.Router = this;
        var mismatch = RouteParser.Mismatch(template, route, args, viewKeys);
        if (mismatch is null)
        {
            // A route registered without a view model has the same key for both: the factory would run twice, and the history
            // would keep two pages per entry. The page is its own view model then, as a cached (scoped or singleton) one already is.
            if (viewModel is null && string.Equals(viewmodelkey, viewkey, StringComparison.Ordinal))
            {
                view ??= provider.GetView(viewkey, args);
                viewModel = view;
            }

            viewModel ??= provider.GetViewModel(viewmodelkey, args) ?? new object();
            view ??= provider.GetView(viewkey, args);
            if (view == null)
                return RouteableResult.Fail(new InvalidOperationException($"No view is registered under the key \"{viewkey}\" (route \"{route}\", template \"{template}\"). Register the page with the same template, or pass the template it was registered with."));

            // What was asked for ("/customers/42", "about"): RouteableUrlSync shows it as the address of the entry.
            // Not viewkey: that is the registration key, i.e. the template ("/customers/{id:int}"), not the address.
            args.Route = route;
            return await GoAsync(view, viewModel, args, cancellationToken);
        }
        return RouteableResult.Fail(new InvalidOperationException(
            $"Route \"{route}\" does not match the template \"{template}\": {mismatch}."));
    }

    private static IRouteableArgs Snapshot(IRouteableArgs? args) => args is null
        ? RouteableArgs.Create()
        : new RouteableArgs
        {
            Route = args.Route,
            Exception = args.Exception,
            ViewModelParameters = [.. args.ViewModelParameters],
            ViewParameters = [.. args.ViewParameters],
            Values = [.. args.Values],
            Router = args.Router,
        };

    /// <inheritdoc />
    public async Task<RouteableResult> GoToAsync(string url, IRouteableArgs? args = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        // A route registered with an explicit template (RegisterRoute(route: "/orders/{id:int}"), [Routeable]): the template
        // reads the parameters from the address.
        var found = provider.GetService<RouteableTable>()?.Entries
            .FirstOrDefault(e => RouteParser.TryMatch(e.Template, url));
        if (found is { Go: not null } hit)
            return await hit.Go(this, url, args ?? RouteableArgs.Create(), cancellationToken);

        // A page of the menu, whose route has no parameter.
        static string Normal(string? route) => (route ?? string.Empty).Split('?')[0].Trim('/');
        if (provider.GetService<IMainRouteables>()?.Routeables.FirstOrDefault(r => string.Equals(Normal(r.Route), Normal(url), StringComparison.OrdinalIgnoreCase)) is { } item)
            return await GoToRouteAsync(item, cancellationToken);

        return RouteableResult.Fail(new InvalidOperationException($"No route matches '{url}'. Register it with an explicit route, for instance RegisterRoute<TView, TViewModel>(route: \"/orders/{{id:int}}\")."));
    }

    /// <inheritdoc />
    public ILockCoordinator? CreateLockCoordinator(IMainRouteables routeables)
    {
        // Locking is optional: without a registered factory (AddLocking) there is nothing to lock.
        return provider.GetService<Func<IMainRouteables, ILockCoordinator>>()?.Invoke(routeables);
    }

    public Task<RouteableResult> GoToRouteAsync(IRouteableEntry routeableView, CancellationToken cancellationToken = default, params string[] viewKeys)
    {
        // A typed entry (RouteableEntry<TViewModel>) names the keys of its view model; any other is bound by its template alone.
        var type = GetRouteableEntryType(routeableView);
        if (type is null)
            return GoToViewAsync(routeableView, routeableView.Template, routeableView.Template, cancellationToken, viewKeys);

        var keys = RouteableKeys.GetKeys(type, routeableView.Route);
        return GoToViewAsync(routeableView, keys.viewKey, keys.viewModelKey, cancellationToken, viewKeys);
    }

    // Goes to the entry, then gives it back the page the router shows so that the next visit finds it.
    private async Task<RouteableResult> GoToViewAsync(
        IRouteableEntry routeableView, string viewKey, string viewModelKey, CancellationToken cancellationToken, string[] viewKeys)
    {
        var result = await GoToRouteAsync(
            routeableView.Arguments.Route ?? routeableView.Route,
            routeableView.Template,
            viewKey,
            viewModelKey,
            routeableView.Arguments,
            Reusable(routeableView.ViewModel),
            Reusable(routeableView.View),
            cancellationToken,
            viewKeys);
        if (result.Success)
        {
            routeableView.View = CurrentView;
            routeableView.ViewModel = CurrentViewModel;
        }
        return result;
    }

    // A menu entry remembers the page it showed last. Not one the router has disposed since: a new one is created instead.
    private object? Reusable(object? remembered) => lifecycle.WasDestroyed(remembered) ? null : remembered;

    // The view model a typed menu entry (RouteableEntry<T>) is for; an entry of another kind is bound by its template alone.
    static Type? GetRouteableEntryType(IRouteableEntry view) => (view as RouteableEntry)?.ViewModelType;

    public Task<RouteableResult> GoToRouteAsync<TViewModel>(IRouteableEntry routeableView, CancellationToken cancellationToken = default, params string[] viewKeys) where TViewModel : class
    {
        var keys = RouteableKeys.GetKeys(typeof(TViewModel), routeableView.Route);
        return GoToViewAsync(routeableView, keys.viewKey, keys.viewModelKey, cancellationToken, viewKeys);
    }

    /// <inheritdoc />
    public Task<RouteableResult> GoToRouteAsync<TViewModel>(string route, IRouteableArgs? args = null, string? template = null, CancellationToken cancellationToken = default, params string[] viewKeys)
    {
        ArgumentNullException.ThrowIfNull(route);
        template ??= TemplateOf(route);

        // Routes are registered under their template (see RegisterRoute), so the keys come from the template.
        var keys = RouteableKeys.GetKeys(typeof(TViewModel), template);

        // RouteParser.TryMatch fills the instance it is given; without one the parsed route values would be lost.
        return GoToRouteAsync(
            route, template, keys.viewKey, keys.viewModelKey,
            args ?? RouteableArgs.Create(), null, null, cancellationToken, viewKeys);
    }
}
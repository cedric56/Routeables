using System.Diagnostics;
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
        => RouteableEvents.Raise(provider, typeof(Router), handlers, sender, args);

    // The route a guard sent the user to instead; read once the gate is released.
    private sealed class Redirection
    {
        public string? Route { get; set; }
    }

    // How many redirects follow one another in the flow of one call: a guard that sends the user back to a page it refuses would loop for ever.
    private const int MaxRedirects = 8;
    private static readonly AsyncLocal<int> Redirects = new();

    // Call only while the gate is held.
    private async Task<RouteableResult> ObserveAsync(
        RouteableKind kind, RouterEntry? from, RouterEntry to,
        CancellationToken ct, Func<Task<RouteableResult>> run, Redirection? redirection = null)
    {
        var started = Stopwatch.GetTimestamp();
        using var activity = RouteableDiagnostics.ActivitySource.StartActivity("Routeables.Route");
        if (activity is not null)
        {
            activity.SetTag("routeables.kind", kind.ToString());
            activity.SetTag("routeables.route", to.Args.Route);
            activity.SetTag("routeables.view_model", to.ViewModel.GetType().Name);
        }

        Raise(Routing, this, new RouteableRoutingEventArgs(kind, from, to));

        RouteableResult result;
        try
        {
            var decision = await AskGuardsAsync(kind, from, to, ct);
            if (decision.IsAllowed)
            {
                result = await run();
            }
            else
            {
                // A redirect is made after this route change ended: the gate is held until then.
                if (decision.RedirectTo is not null && redirection is not null)
                    redirection.Route = decision.RedirectTo;
                result = RouteableResult.Cancel();
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            Finish(activity, started, kind, RouteableOutcome.Cancelled);
            Raise(Routed, this, new RouteableRoutedEventArgs(kind, from, to, RouteableOutcome.Cancelled));
            throw;
        }
        catch (Exception ex)
        {
            Finish(activity, started, kind, RouteableOutcome.Failed);
            Raise(Routed, this, new RouteableRoutedEventArgs(kind, from, to, RouteableOutcome.Failed, ex));
            throw;
        }

        var outcome = result.Success ? RouteableOutcome.Success
                    : result.Cancelled ? RouteableOutcome.Cancelled
                    : RouteableOutcome.Failed;
        Finish(activity, started, kind, outcome);
        Raise(Routed, this, new RouteableRoutedEventArgs(kind, from, to, outcome));
        return result;
    }

    private static void Finish(Activity? activity, long started, RouteableKind kind, RouteableOutcome outcome)
    {
        if (RouteableDiagnostics.RouteDuration.Enabled)
        {
            RouteableDiagnostics.RouteDuration.Record(
                Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                new KeyValuePair<string, object?>("routeables.kind", kind.ToString()),
                new KeyValuePair<string, object?>("routeables.outcome", outcome.ToString()));
        }

        if (activity is not null)
        {
            activity.SetTag("routeables.outcome", outcome.ToString());
            activity.SetStatus(outcome == RouteableOutcome.Failed ? ActivityStatusCode.Error : ActivityStatusCode.Ok);
        }
    }

    // Every registered guard, in the order of registration, until one refuses.
    private async Task<RouteGuardDecision> AskGuardsAsync(RouteableKind kind, RouterEntry? from, RouterEntry to, CancellationToken ct)
    {
        var guards = provider.GetServices<IRouteGuard>().ToArray();
        if (guards.Length == 0)
            return RouteGuardDecision.Allow;

        var change = new RouteableRoutingEventArgs(kind, from, to);
        foreach (var guard in guards)
        {
            var decision = await guard.CanEnterAsync(change, ct) ?? RouteGuardDecision.Allow;
            if (!decision.IsAllowed)
                return decision;
        }

        return RouteGuardDecision.Allow;
    }

    // Goes where a guard sent the user, once the route change that was refused has ended and released the gate.
    private async Task<RouteableResult> RedirectAsync(string route, CancellationToken ct)
    {
        if (Redirects.Value >= MaxRedirects)
            return RouteableResult.Fail(new InvalidOperationException(
                $"More than {MaxRedirects} redirects in a row, the last one to \"{route}\": a route guard sends the user to a route that another guard (or the same one) refuses."));

        RouteableDiagnostics.Redirects.Add(1, new KeyValuePair<string, object?>("routeables.route", route));

        Redirects.Value++;
        try
        {
            return await GoToAsync(route, null, null, ct);
        }
        finally
        {
            Redirects.Value--;
        }
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

    public async Task<RouteableResult> GoAsync(object view, object viewModel, IRouteableArgs? args = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(viewModel);

        var redirection = new Redirection();
        var result = await GoCoreAsync(view, viewModel, args, redirection, cancellationToken);

        // The gate is free: a guard that sent the user elsewhere has its route change now.
        return redirection.Route is { } route ? await RedirectAsync(route, cancellationToken) : result;
    }

    private async Task<RouteableResult> GoCoreAsync(object view, object viewModel, IRouteableArgs? args, Redirection redirection, CancellationToken cancellationToken)
    {
        var enter = gate.EnterAsync(cancellationToken);
        try { await enter; } catch { gate.Abort(); throw; }

        try
        {
            IRouteableFor? viewFor = view as IRouteableFor;
            var previousContext = viewFor?.Context;    // inside the try: a throwing getter must not leave the gate held
            args ??= RouteableArgs.Create();

            var previous = history.Current;
            var entering = new RouterEntry(viewModel, view, args);

            return await ObserveAsync(RouteableKind.Push, previous, entering, cancellationToken, redirection: redirection, run: async () =>
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
            // What this call builds (not what the caller handed over, which it keeps): if the route change does not show it
            // (a guard or CanLeave refused, a redirect, an exception), nobody else would ever dispose it.
            var built = new List<object>();
            if (viewModel is null && string.Equals(viewmodelkey, viewkey, StringComparison.Ordinal))
            {
                if (view is null && provider.GetView(viewkey, args) is { } own) { view = own; built.Add(own); }
                viewModel = view;
            }

            if (viewModel is null)
            {
                viewModel = provider.GetViewModel(viewmodelkey, args) ?? new object();
                built.Add(viewModel);
            }
            if (view is null && provider.GetView(viewkey, args) is { } created) { view = created; built.Add(created); }
            if (view == null)
            {
                await DiscardAsync(built);
                return RouteableResult.Fail(new InvalidOperationException($"No view is registered under the key \"{viewkey}\" (route \"{route}\", template \"{template}\"). Register the page with the same template, or pass the template it was registered with."));
            }

            // What was asked for ("/customers/42", "about"): RouteableUrlSync shows it as the address of the entry.
            // Not viewkey: that is the registration key, i.e. the template ("/customers/{id:int}"), not the address.
            args.Route = route;
            try
            {
                return await GoAsync(view, viewModel, args, cancellationToken);
            }
            finally
            {
                // The usual end: the page this call built is the one shown, so there is nothing to look at.
                if (!IsShown(built))
                    await DiscardAsync(built);
            }
        }
        return RouteableResult.Fail(new InvalidOperationException(
            $"Route \"{route}\" does not match the template \"{template}\": {mismatch}."));
    }

    private bool IsShown(List<object> built)
    {
        if (history.Current is not { } current)
            return false;
        foreach (var instance in built)
            if (ReferenceEquals(instance, current.View) || ReferenceEquals(instance, current.ViewModel))
                return true;
        return false;
    }

    // Disposes what a route change built and did not put in the history. Pages that stay (in the history, or in the cache
    // of a scoped or singleton registration) are kept: DestroyAsync skips them.
    private async Task DiscardAsync(List<object> built)
    {
        if (built.Count == 0) return;
        try
        {
            var unused = built
                .Select(instance => new RouterEntry(instance, instance, RouteableArgs.Create()))
                .ToArray();
            await lifecycle.DestroyAsync(unused, history.Snapshot());
        }
        catch (Exception ex) { RouteableErrors.Report<Router>(provider, ex); }
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
            Region = args.Region,
            Transition = args.Transition,
        };

    /// <inheritdoc />
    public async Task<RouteableResult> GoToAsync(
        string url, IRouteableArgs? args = null, string? template = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        // A route registered with an explicit template (RegisterRoute(route: "/orders/{id:int}"), [Routeable]): the template
        // reads the parameters from the address. Given a template, it is the registered route with that template.
        var found = default((string Template, Type? ViewModelType));
        foreach (var entry in provider.GetService<RouteableTable>()?.Entries ?? [])
        {
            if (template is null ? RouteParser.TryMatch(entry.Template, url) : string.Equals(entry.Template, template, StringComparison.OrdinalIgnoreCase))
            {
                found = entry;
                break;
            }
        }
        if (found.Template is not null)
        {
            var keys = found.ViewModelType is null
                ? (viewKey: found.Template, viewModelKey: found.Template)
                : RouteableKeys.GetKeys(found.ViewModelType, found.Template);
            // RouteParser.TryMatch fills the instance it is given; without one the parsed route values would be lost.
            return await GoToRouteAsync(url, found.Template, keys.viewKey, keys.viewModelKey, args ?? RouteableArgs.Create(), null, null, cancellationToken);
        }

        // A template that is not registered is the route itself: the view and the view model are found under it.
        if (template is not null)
            return await GoToRouteAsync(url, template, template, template, args, null, null, cancellationToken);

        // A page of the menu, whose route has no parameter.
        static string Normal(string? route) => (route ?? string.Empty).Split('?')[0].Trim('/');
        if (provider.GetService<IMainViewModel>()?.Routeables.FirstOrDefault(r => string.Equals(Normal(r.Route), Normal(url), StringComparison.OrdinalIgnoreCase)) is { } item)
            return await GoToEntryAsync(item, cancellationToken, []);

        return RouteableResult.Fail(new InvalidOperationException($"No route matches '{url}'. Register it with an explicit route, for instance RegisterRoute<TView, TViewModel>(route: \"/orders/{{id:int}}\")."));
    }

    /// <inheritdoc />
    public ILockCoordinator? CreateLockCoordinator(IMainViewModel mainViewModel)
    {
        // Locking is optional: without a registered factory (AddLocking) there is nothing to lock.
        return provider.GetService<Func<IMainViewModel, ILockCoordinator>>()?.Invoke(mainViewModel);
    }

    /// <inheritdoc />
    public Task<RouteableResult> GoToRouteAsync(RouteableRouteRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var viewKeys = request.ViewKeys ?? [];

        if (request.Entry is { } entry)
            return request.ViewModelType is { } entryType
                ? GoToEntryAsync(entry, entryType, cancellationToken, viewKeys)
                : GoToEntryAsync(entry, cancellationToken, viewKeys);

        if (string.IsNullOrWhiteSpace(request.Route) || request.ViewModelType is null)
            throw new ArgumentException("A route request needs an entry, or a route and a view model type.", nameof(request));

        return GoToTypedRouteAsync(request.ViewModelType, request.Route, request.Args, request.Template, cancellationToken, viewKeys);
    }

    private Task<RouteableResult> GoToEntryAsync(IRouteableEntry routeableView, CancellationToken cancellationToken, string[] viewKeys)
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
        // Only the page this entry asked for: a guard may have sent the user elsewhere (a sign-in), and the entry must not remember that page.
        if (result.Success && WentTo(routeableView))
        {
            routeableView.View = CurrentView;
            routeableView.ViewModel = CurrentViewModel;
        }
        return result;
    }

    // Whether the page shown is the one of the entry (its route), and not the one a guard redirected to.
    internal bool WentTo(IRouteableEntry entry)
        => string.Equals(history.Current?.Args.Route, entry.Arguments.Route ?? entry.Route, StringComparison.Ordinal);

    // A menu entry remembers the page it showed last. Not one the router has disposed since: a new one is created instead.
    private object? Reusable(object? remembered) => lifecycle.WasDestroyed(remembered) ? null : remembered;

    // The view model a typed menu entry (RouteableEntry<T>) is for; an entry of another kind is bound by its template alone.
    static Type? GetRouteableEntryType(IRouteableEntry view) => (view as RouteableEntry)?.ViewModelType;

    private Task<RouteableResult> GoToTypedRouteAsync(
        Type viewModelType, string route, IRouteableArgs? args, string? template, CancellationToken cancellationToken, string[] viewKeys)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(route);
        template ??= TemplateOf(route);

        // Routes are registered under their template (see RegisterRoute), so the keys come from the template.
        var keys = RouteableKeys.GetKeys(viewModelType, template);

        // RouteParser.TryMatch fills the instance it is given; without one the parsed route values would be lost.
        return GoToRouteAsync(
            route, template, keys.viewKey, keys.viewModelKey,
            args ?? RouteableArgs.Create(), null, null, cancellationToken, viewKeys);
    }

    // The template registered for a route: the one that is the route, the one that matches it, the one of a menu entry, or the route itself.
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

        var item = provider.GetService<IMainViewModel>()?.Routeables
            .FirstOrDefault(r => string.Equals(Normal(r.Route), Normal(route), StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(r.Template));
        return item?.Template ?? route;
    }

    private Task<RouteableResult> GoToEntryAsync(IRouteableEntry routeableView, Type viewModelType, CancellationToken cancellationToken, string[] viewKeys)
    {
        var keys = RouteableKeys.GetKeys(viewModelType, routeableView.Route);
        return GoToViewAsync(routeableView, keys.viewKey, keys.viewModelKey, cancellationToken, viewKeys);
    }
}

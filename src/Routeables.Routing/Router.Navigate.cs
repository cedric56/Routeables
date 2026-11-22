using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Errors;
using Routeables.Abstraction.Lifecycle;
using Routeables.Abstraction.Registrations;
using Routeables.Abstraction.Routing;

namespace Routeables.Routing;

// Opening a page: from a view and a view model, from a registered route, from a request.
internal partial class Router
{
    public async Task<RouteableResult> GoAsync(object view, object viewModel, IRouteableArgs? args = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(viewModel);

        var redirection = new Redirection();
        var result = await GoCoreAsync(view, viewModel, args, redirection, cancellationToken);

        // The gate is free: a guard that redirected the user gets its navigation now.
        return redirection.Route is { } route ? await RedirectAsync(route, cancellationToken) : result;
    }

    private async Task<RouteableResult> GoCoreAsync(object view, object viewModel, IRouteableArgs? args, Redirection redirection, CancellationToken cancellationToken)
    {
        return await RunGatedAsync(cancellationToken, async token =>
        {
            IRouteableFor? viewFor = view as IRouteableFor ?? provider.GetService<IRouteableViewBinder>()?.Adapt(view);
            var previousContext = viewFor?.Context; // read inside the gated body, so that a throwing getter cannot leave the gate held
            args ??= RouteableArgs.Create();

            var previous = history.Current;
            var entering = new RouterEntry(viewModel, view, args);

            return await ObserveAsync(RouteableKind.Push, previous, entering, token, redirection: redirection, run: async () =>
            {
                try
                {
                    // 1. Can the current page be left?
                    if (previous is not null && !await lifecycle.CanLeaveAsync(previous, token))
                        return RouteableResult.Cancel();

                    // 2. Bind view and view model.
                    if (viewFor is not null)
                        viewFor.Context = viewModel;

                    // 3. Initialize the target (view model first, then view). Nothing has been torn down yet.
                    await lifecycle.InitializeAsync(entering, token);

                    // 4. Leave the current page and enter the target (or just enter, on the first navigation).
                    if (previous is not null)
                        await lifecycle.TransitionAsync(previous, entering, token);
                    else
                        await lifecycle.EnterAsync(entering, token);

                    // 5. Commit, then destroy what the push cut off (forward entries, the oldest evicted entry).
                    var removed = history.Push(entering);
                    await lifecycle.NotifyEnteredAsync(previous, entering, token);
                    if (removed.Count > 0) await lifecycle.DestroyAsync(removed, history.Snapshot());
                    return RouteableResult.Ok();
                }
                catch
                {
                    if (viewFor is not null)
                        viewFor.Context = previousContext; // restored before Navigated(Failed) is raised
                    throw;
                }
            });
        });
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
        Type? viewModelType = null,
        params string[] viewKeys)
    {
        // One navigation, one arguments instance. The caller's instance (RouteableMenuEntry.Arguments is reused on every click) is only a
        // template: TryMatch appends the route values to its bags and Route is written, so two history entries sharing it would see each
        // other's values (doubled constructor arguments, a duplicate key, the wrong address on Back).
        args = Snapshot(args);
        // The view model built by this navigation receives this router if it asks for an IRouter (a region is not the default router).
        args.Router = this;
        var mismatch = RouteParser.Mismatch(template, route, args, viewKeys);
        if (mismatch is null)
        {
            // A route registered without a view model has the same key for both: the factory would run twice and the history would keep two
            // pages per entry. The page is then its own view model, as a cached (scoped or singleton) one already is.
            // This covers what this call builds, not what the caller handed over. If the navigation does not show it (a guard or CanLeave
            // refused, a redirect, an exception), nobody else would ever dispose it.
            // A page about to be built is first offered to the guards that need only its type, so a refused visitor costs no constructor call.
            if (viewModel is null && await AskTypeGuardsAsync(template, route, viewModelType, cancellationToken) is { } refused)
                return refused;

            var built = new List<object>();
            try
            {
                if (viewModel is null && string.Equals(viewmodelkey, viewkey, StringComparison.Ordinal))
                {
                    if (view is null && provider.GetView(viewkey, args) is { } own) { view = own; built.Add(own); }
                    viewModel = view;
                }

                if (viewModel is null)
                {
                    // A view declared without a view model (a Razor component, for instance) is shown with an empty object as its context. This is intended,
                    // not a failure.
                    viewModel = provider.GetViewModel(viewmodelkey, args) ?? new object();
                    built.Add(viewModel);
                }
                if (view is null && provider.GetView(viewkey, args) is { } created) { view = created; built.Add(created); }
            }
            catch
            {
                // The view model was built but the view could not be: nothing will show it, so nobody else would dispose it.
                await DiscardAsync(built);
                throw;
            }
            if (view == null)
            {
                await DiscardAsync(built);
                return RouteableResult.Fail(new InvalidOperationException($"No view is registered under the key \"{viewkey}\" (route \"{route}\", template \"{template}\"). Register the page with the same template, or pass the template it was registered with."));
            }

            // What was asked for ("/customers/42", "about"): RouteableUrlSync shows it as the entry's address.
            // Not the view key, which is the registration key, i.e. the template ("/customers/{id:int}"), not the address.
            args.Route = route;
            try
            {
                return await GoAsync(view, viewModel, args, cancellationToken);
            }
            finally
            {
                // The usual outcome: the page this call built is the one shown, so there is nothing to clean up.
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

    // Dispose what a navigation built and did not put in the history. Pages that stay (in the history, or in the cache of a scoped or
    // singleton registration) are kept: DestroyAsync skips them.
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
    public Task<RouteableResult> GoToRouteAsync(RouteableRouteRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var viewKeys = request.ViewKeys ?? [];

        if (request.Entry is { } entry)
            return request.ViewModelType is { } entryType
                ? GoToEntryAsync(entry, entryType, cancellationToken, viewKeys)
                : GoToEntryAsync(entry, cancellationToken, viewKeys);

        if (string.IsNullOrWhiteSpace(request.Route) && request.ViewModelType is { } byType)
            return GoToRegisteredAsync(byType, request, viewKeys, cancellationToken);

        if (string.IsNullOrWhiteSpace(request.Route) || request.ViewModelType is null)
            throw new ArgumentException("A route request needs an entry, a route and a view model type, or a view model type with a registered route.", nameof(request));

        return GoToTypedRouteAsync(request.ViewModelType, request.Route, request.Args, request.Template, cancellationToken, viewKeys);
    }

    // A request with a view model type and no route: use the template registered for the view model, filled with the values.
    private Task<RouteableResult> GoToRegisteredAsync(Type viewModelType, RouteableRouteRequest request, string[] viewKeys, CancellationToken cancellationToken)
    {
        var entries = provider.GetService<RouteableTable>()?.Entries ?? [];
        var templates = entries.Where(e => e.ViewModelType == viewModelType).Select(e => e.Template).ToList();
        if (templates.Count == 0)
            return Task.FromResult(RouteableResult.Fail(new InvalidOperationException(
                $"No route is registered for {viewModelType.Name}. Give it one: [Routeable(\"/path/{{id:int}}\")] on the view model, or RegisterRoute<TView, {viewModelType.Name}>(route: \"/path\").")));

        // Several routes for one view model: the one that takes the most of the values wins ("/orders/{id}" for an id, not "/orders/{status?}/{page?}",
        // which would drop it); equal ones keep the order of registration. When none can be filled, the error of the last one is reported.
        Exception? failure = null;
        string? best = null, bestRoute = null;
        var bestUsed = -1;
        foreach (var template in templates)
        {
            try
            {
                var route = RouteParser.Build(template, request.Values);
                var used = RouteParser.UsedBy(template, request.Values);
                if (used > bestUsed) { best = template; bestRoute = route; bestUsed = used; }
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or System.Text.Json.JsonException) { failure = ex; }
        }

        return best is null
            ? Task.FromResult(RouteableResult.Fail(failure!))
            : GoToTypedRouteAsync(viewModelType, bestRoute!, request.Args, best, cancellationToken, viewKeys);
    }

    // Go to the entry, then hand it the page the router shows, so that its next visit finds it.
    private async Task<RouteableResult> GoToViewAsync(
        IRouteableMenuEntry routeableView, string viewKey, string viewModelKey, CancellationToken cancellationToken, string[] viewKeys, IRouteableArgs? supplied = null, Type? viewModelType = null, string? address = null)
    {
        var route = address ?? routeableView.Arguments.Route ?? routeableView.Route;
        var result = await GoToRouteAsync(
            route,
            routeableView.Template,
            viewKey,
            viewModelKey,
            supplied ?? routeableView.Arguments,
            Reusable(routeableView.ViewModel),
            Reusable(routeableView.View),
            cancellationToken,
            viewModelType,
            viewKeys: viewKeys);
        // Only the page this entry asked for: a guard may have redirected the user elsewhere (a sign-in), and the entry must not remember that page.
        if (result.Success && WentTo(route))
        {
            routeableView.View = CurrentView;
            routeableView.ViewModel = CurrentViewModel;
        }
        return result;
    }

    // Whether the page shown is the entry's page (its route), and not one a guard redirected to.
    private bool WentTo(string route)
        => RouteAddress.SameFor(this, history.Current?.Args.Route, route);

    // A menu entry remembers the page it showed last, unless the router has disposed it since: then a new one is created.
    private object? Reusable(object? remembered) => lifecycle.WasDestroyed(remembered) ? null : remembered;

    private Task<RouteableResult> GoToTypedRouteAsync(
        Type viewModelType, string route, IRouteableArgs? args, string? template, CancellationToken cancellationToken, string[] viewKeys)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(route);
        template ??= TemplateOf(route);

        // Routes are registered under their template (see RegisterRoute), so the keys come from the template.
        var keys = RouteableKeys.GetKeys(viewModelType, template);

        // RouteParser.TryMatch fills the instance it is given; without one, the parsed route values would be lost.
        return GoToRouteAsync(
            route, template, keys.viewKey, keys.viewModelKey,
            args ?? RouteableArgs.Create(), null, null, cancellationToken, viewModelType, viewKeys: viewKeys);
    }
}

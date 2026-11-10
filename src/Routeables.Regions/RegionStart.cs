using Routeables.Abstraction;
using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Errors;
using Routeables.Abstraction.Routing;
using System.Runtime.CompilerServices;

namespace Routeables.Regions;

/// <summary>
/// What the <c>RouteableRegion</c> of each platform does when it appears: it starts the routing where the pages are shown. A region that
/// has a <c>Route</c> opens that page when its router shows nothing yet; the default region (the one with no name) without a route asks
/// the main view model to start (<see cref="IMainViewModel.Start"/>), which an application without a menu answers by opening "/".
/// A named region without a route starts nothing: a page routes it. The start happens once for a router, so a region that is attached
/// again, or two regions that show the same router, do not open the page twice.
/// </summary>
public static class RegionStart
{
    private sealed class State
    {
        public string? Route;
        public bool Started;
    }

    private static readonly ConditionalWeakTable<IRouter, State> States = new();

    /// <summary>
    /// Starts the routing of the region when it has something to start.
    /// </summary>
    /// <param name="provider">The container of the application.</param>
    /// <param name="region">The name of the region, or <see langword="null"/> (or empty) for the default one.</param>
    /// <param name="route">The page the region opens at start, or <see langword="null"/>.</param>
    /// <param name="wait">
    /// <see langword="true"/> to only remember <paramref name="route"/>: the host starts later with <see cref="StartDefault"/> (Blazor,
    /// where the address of the browser decides the first page before the home route is used).
    /// </param>
    public static void Begin(IServiceProvider provider, string? region, string? route = null, bool wait = false)
    {
        ArgumentNullException.ThrowIfNull(provider);

        var name = string.IsNullOrEmpty(region) ? null : region;
        try
        {
            var router = provider.GetRouter(name);
            var state = States.GetOrCreateValue(router);
            string? chosen;
            lock (state)
            {
                if (!string.IsNullOrEmpty(route))
                    state.Route = route;

                chosen = state.Route;
                if (wait || state.Started || router.CurrentView is not null || (name is not null && chosen is null))
                    return;

                state.Started = true;
            }

            Run(provider, router, name, chosen);
        }
        catch (Exception ex)    // raised from an event: it must not end the application
        {
            RouteableErrors.Report(provider, ex, typeof(RegionStart));
        }
    }

    /// <summary>
    /// Starts the default region with the route it declared, or through the main view model when it declared none. For the host that
    /// waited (<c>wait: true</c>) once it knows that no address chose the first page.
    /// </summary>
    /// <param name="provider">The container of the application.</param>
    public static void StartDefault(IServiceProvider provider) => Begin(provider, null);

    private static void Run(IServiceProvider provider, IRouter router, string? name, string? route)
    {
        if (route is null)
        {
            if (provider.GetService(typeof(IMainViewModel)) is IMainViewModel main)
                main.Start();
            return;
        }

        RouteableHelper.FireAndForget(async () =>
        {
            var result = await router.GoToAsync(route);
            if (!result.Success)
            {
                RouteableErrors.Report(provider, result.Exception ?? new InvalidOperationException($"The route '{route}' could not be opened."),
                    typeof(RegionStart), route);
                return;
            }

            // The menu, or the layout that follows the main view model, shows the page that is now current.
            if (name is null && provider.GetService(typeof(IMainViewModel)) is IMainViewModel main)
                main.OnRouterMoved(router.CurrentViewModel);
        }, provider, route);
    }
}

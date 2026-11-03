namespace Routeables.Abstraction;

/// <summary>
/// The <c>GoToRouteAsync</c> methods of <see cref="IRouter"/>. They are extensions so the interface stays small: the router
/// implements one member (<see cref="IRouter.GoToRouteAsync(RouteableRouteRequest, CancellationToken)"/>) and these build the request.
/// They are in the namespace of <see cref="IRouter"/>: nothing more to import.
/// </summary>
public static class RouterExtensions
{
    /// <summary>
    /// Goes to <paramref name="route"/> with <typeparamref name="TViewModel"/> as the view model: <c>GoToRouteAsync&lt;MenuViewModel&gt;("MenuView")</c>.
    /// The view is the one registered under the route, the view model is built from <typeparamref name="TViewModel"/>. The
    /// <paramref name="template"/> is optional: without it, it is the one registered for the route (<c>RouteableTable</c>, then the
    /// main routeables), or the route itself.
    /// </summary>
    /// <typeparam name="TViewModel">The type of the view model to build.</typeparam>
    /// <param name="router">The router.</param>
    /// <param name="route">The route to go to, with its values (<c>"/orders/42"</c>).</param>
    /// <param name="args">The arguments of the route change, or <see langword="null"/> for none.</param>
    /// <param name="template">The registered template, when the route alone does not tell it.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <param name="viewKeys">Names of the route values that go to the view (a property of the view marked <c>[Parameter]</c>, <c>[QueryProperty]</c> or <c>[RouteableParameter]</c>) instead of the view model.</param>
    public static Task<RouteableResult> GoToRouteAsync<TViewModel>(
        this IRouter router,
        string route,
        IRouteableArgs? args = null,
        string? template = null,
        CancellationToken cancellationToken = default,
        params string[] viewKeys)
        where TViewModel : class
    {
        ArgumentNullException.ThrowIfNull(router);
        ArgumentException.ThrowIfNullOrWhiteSpace(route);
        return router.GoToRouteAsync(
            new RouteableRouteRequest { Route = route, Template = template, ViewModelType = typeof(TViewModel), Args = args, ViewKeys = viewKeys },
            cancellationToken);
    }

    /// <summary>Goes to a menu entry, with <typeparamref name="TViewModel"/> as the view model.</summary>
    /// <typeparam name="TViewModel">The type of the view model to build.</typeparam>
    /// <param name="router">The router.</param>
    /// <param name="routeableView">The entry to go to.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <param name="viewKeys">Names of the route values that go to the view instead of the view model.</param>
    public static Task<RouteableResult> GoToRouteAsync<TViewModel>(
        this IRouter router,
        IRouteableEntry routeableView,
        CancellationToken cancellationToken = default,
        params string[] viewKeys)
        where TViewModel : class
    {
        ArgumentNullException.ThrowIfNull(router);
        ArgumentNullException.ThrowIfNull(routeableView);
        return router.GoToRouteAsync(
            new RouteableRouteRequest { Entry = routeableView, ViewModelType = typeof(TViewModel), ViewKeys = viewKeys },
            cancellationToken);
    }

    /// <summary>Goes to a menu entry, which knows its route, its template and its view model.</summary>
    /// <param name="router">The router.</param>
    /// <param name="routeableView">The entry to go to.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <param name="viewKeys">Names of the route values that go to the view instead of the view model.</param>
    public static Task<RouteableResult> GoToRouteAsync(
        this IRouter router,
        IRouteableEntry routeableView,
        CancellationToken cancellationToken = default,
        params string[] viewKeys)
    {
        ArgumentNullException.ThrowIfNull(router);
        ArgumentNullException.ThrowIfNull(routeableView);
        return router.GoToRouteAsync(
            new RouteableRouteRequest { Entry = routeableView, ViewKeys = viewKeys },
            cancellationToken);
    }
}

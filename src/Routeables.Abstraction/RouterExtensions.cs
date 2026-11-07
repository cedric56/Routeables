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
        IMenuEntry routeableView,
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
        IMenuEntry routeableView,
        CancellationToken cancellationToken = default,
        params string[] viewKeys)
    {
        ArgumentNullException.ThrowIfNull(router);
        ArgumentNullException.ThrowIfNull(routeableView);
        return router.GoToRouteAsync(
            new RouteableRouteRequest { Entry = routeableView, ViewKeys = viewKeys },
            cancellationToken);
    }

    /// <summary>
    /// Goes to the page of <typeparamref name="TViewModel"/> without writing its route: the router finds the template registered for the
    /// view model (<c>[Routeable("/orders/{id:int}")]</c>, <c>RegisterRoute</c>) and fills it from <paramref name="values"/>:
    /// <c>router.GoToAsync&lt;OrderViewModel&gt;(new Dictionary&lt;string, object?&gt; { ["id"] = 42 })</c>. It works from anywhere that has an
    /// <see cref="IRouter"/>, and reads no property by reflection (trim and AOT safe); with an object of values, see the overload that takes one.
    /// </summary>
    /// <typeparam name="TViewModel">The destination view model; a route must be registered for it.</typeparam>
    /// <param name="router">The router.</param>
    /// <param name="values">The values of the placeholders by name (not case-sensitive), or <see langword="null"/> for a route without any.</param>
    /// <param name="args">The arguments of the route change, or <see langword="null"/> for none.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The result; it fails when no route is registered for the view model or a required value is missing.</returns>
    public static Task<RouteableResult> GoToAsync<TViewModel>(
        this IRouter router,
        IReadOnlyDictionary<string, object?>? values = null,
        IRouteableArgs? args = null,
        CancellationToken cancellationToken = default)
        where TViewModel : class
    {
        ArgumentNullException.ThrowIfNull(router);
        return router.GoToRouteAsync(
            new RouteableRouteRequest { ViewModelType = typeof(TViewModel), Values = values, Args = args },
            cancellationToken);
    }

    /// <summary>
    /// Like the overload with a dictionary, with the values as the properties of an object:
    /// <c>router.GoToAsync&lt;OrderViewModel&gt;(new { id = 42 })</c>. The properties are read by reflection, so a trimmed or Native AOT
    /// application keeps them itself (or uses the dictionary).
    /// </summary>
    /// <typeparam name="TViewModel">The destination view model; a route must be registered for it.</typeparam>
    /// <param name="router">The router.</param>
    /// <param name="values">An anonymous object or a record: one property per placeholder, names not case-sensitive.</param>
    /// <param name="args">The arguments of the route change, or <see langword="null"/> for none.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The result; it fails when no route is registered for the view model or a required value is missing.</returns>
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Reads the properties of the values object by reflection; use the dictionary overload in a trimmed application.")]
    public static Task<RouteableResult> GoToAsync<TViewModel>(
        this IRouter router,
        object values,
        IRouteableArgs? args = null,
        CancellationToken cancellationToken = default)
        where TViewModel : class
    {
        ArgumentNullException.ThrowIfNull(router);
        ArgumentNullException.ThrowIfNull(values);
        return router.GoToRouteAsync(
            new RouteableRouteRequest { ViewModelType = typeof(TViewModel), Values = values, Args = args },
            cancellationToken);
    }
}

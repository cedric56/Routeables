using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Injections;
using Routeables.Abstraction.Lifecycle;
using Routeables.Abstraction.Locking;
using Routeables.Abstraction.Registrations;
using System.Diagnostics.CodeAnalysis;

namespace Routeables.Abstraction.Routing;

/// <summary>
/// The <c>GoToRouteAsync</c> methods of <see cref="IRouter"/>. They are extensions so the interface stays small: the router
/// implements one member (<see cref="IRouter.GoToRouteAsync(RouteableRouteRequest, CancellationToken)"/>) and these build the request.
/// They are in the namespace of <see cref="IRouter"/>: nothing more to import.
/// </summary>
public static class RouterExtensions
{
    /// <summary>
    /// The router of a region: the one registered with <c>AddRegion</c> under <paramref name="region"/>, or the default
    /// router when <paramref name="region"/> is <see langword="null"/> or blank.
    /// </summary>
    /// <param name="provider">The provider (scope) to resolve from.</param>
    /// <param name="region">The region's identifier; <see langword="null"/> for the default router.</param>
    /// <exception cref="InvalidOperationException">No region of that name is registered (see <c>AddRegion</c>).</exception>
    public static IRouter GetRouter(this IServiceProvider provider, string? region)
    {
        ArgumentNullException.ThrowIfNull(provider);
        if (string.IsNullOrWhiteSpace(region))
            return provider.GetRequiredService<IRouter>();

        // The container's own message does not say which key is missing: name the region and how to register it.
        return provider.GetKeyedService<IRouter>(region)
            ?? throw new InvalidOperationException(
                $"No region named '{region}' is registered. Register it with services.AddRegion(\"{region}\"), or use a region name that is registered.");
    }

    /// <summary>
    /// Empties the histories of <paramref name="regions"/>: their pages are disposed and each region starts again from nothing.
    /// Use it to leave an application made of several regions, or to start one over.
    /// </summary>
    public static async Task EraseRegionsAsync(
        this IServiceProvider provider, IEnumerable<string> regions, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(regions);
        foreach (var region in regions)
            await provider.GetRequiredKeyedService<IRouter>(region).EraseHistoryAsync(cancellationToken);
    }

    /// <summary>
    /// Goes to a registered route by its address (<c>provider.GoToAsync("/orders/42")</c>), in the router of
    /// <paramref name="region"/>, or else the <see cref="IRouteableArgs.Region"/> of <paramref name="args"/> (the default router when
    /// neither is given). See <see cref="IRouter.GoToAsync"/>.
    /// </summary>
    public static Task<RouteableResult> GoToAsync(
        this IServiceProvider provider, string url, string? region = null, IRouteableArgs? args = null, string? template = null, CancellationToken cancellationToken = default)
        => provider.GetRouter(region ?? args?.Region).GoToAsync(url, args, template, cancellationToken);

    /// <summary>Goes to a registered route by its address, in the region named by <paramref name="args"/> (<c>RouteableArgs.Create().WithRegion("Form")</c>).</summary>
    public static Task<RouteableResult> GoToAsync(
        this IServiceProvider provider, string url, IRouteableArgs args, string? template = null, CancellationToken cancellationToken = default)
        => provider.GoToAsync(url, region: null, args, template, cancellationToken);

    /// <summary>The current view model as a <typeparamref name="T"/>, or <see langword="null"/> when it is another type or none.</summary>
    public static T? GetCurrentViewModel<T>(this IRouter router) where T : class
        => router.CurrentViewModel as T;

    /// <summary>The coordinator that locks <paramref name="mainViewModel"/> while a page blocks leaving; <see langword="null"/> when the router offers none.</summary>
    public static IRouteableLockCoordinator? CreateLockCoordinator(this IRouter router, IMainViewModel mainViewModel)
        => (router as IRouteableLockCoordinatorFactory)?.CreateLockCoordinator(mainViewModel);

    /// <summary>The router that shows <paramref name="view"/>: the one of its <see cref="IRouteableMenuEntry.Region"/>.</summary>
    public static IRouter RouterFor(this IServiceProvider provider, IRouteableMenuEntry view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return provider.GetRouter(view.Region);
    }

    /// <summary>
    /// Shows <paramref name="view"/> in its region (the default router if it names none). A <c>RouteableRegion</c> given the
    /// same name displays it: the call does not need to know where on screen that is.
    /// </summary>
    public static Task<RouteableResult> GoToAsync(
        this IServiceProvider provider, IRouteableMenuEntry view, CancellationToken cancellationToken = default)
        => provider.RouterFor(view).GoToRouteAsync(view, cancellationToken);

    /// <summary>
    /// Registers <typeparamref name="T"/> as a DI-scoped service, once, if no registration for it
    /// exists yet. Use this for state that must not leak between circuits/scopes
    /// (e.g. <see cref="ScopedViewCache"/>).
    /// </summary>
    internal static void EnsureScoped<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(this IServiceCollection services) where T : class
    {
        if (!services.Any(d => d.ServiceType == typeof(T)))
            services.AddScoped<T>();
    }

    /// <summary>
    /// Creates the view and view model registered for <typeparamref name="TViewModel"/>, binds them
    /// together and shows the view modally.
    /// </summary>
    /// <typeparam name="TViewModel">The view model type; its view must implement <see cref="IRouteableModalFor{TViewModel, TResult}"/>.</typeparam>
    /// <typeparam name="TResult">The result type the modal returns; must match the view's declared result type.</typeparam>
    /// <param name="provider">The provider used to resolve the view and view model.</param>
    /// <param name="args">Optional view / view-model parameters.</param>
    /// <param name="route">Optional route override used to compute the registration keys.</param>
    /// <returns>A task completing with the modal's result (or <c>null</c>/default if dismissed).</returns>
    /// <exception cref="InvalidOperationException">No suitable view is registered, or it is not a modal for the requested types.</exception>
    public static Task<TResult?> ShowModalAsync<
        [DynamicallyAccessedMembers(InjectableMembers.Created)] TViewModel, TResult>(
        this IServiceProvider provider,
        IRouteableArgs? args = null,
        string? route = null) where TViewModel : class
    {
        var keys = RouteableKeys.GetKeys(typeof(TViewModel), route);

        var viewModel = provider.GetViewModel<TViewModel>(keys.viewModelKey, args);
        var view = provider.GetModalFor<TViewModel, TResult>(args, keys.viewKey) ?? throw new InvalidOperationException($"Unable to create view for {typeof(TViewModel).Name}.  Ensure that it is registered in the container.");
        view.Context = viewModel;
        return view.ShowModalAsync();
    }

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
        IRouteableMenuEntry routeableView,
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
        IRouteableMenuEntry routeableView,
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

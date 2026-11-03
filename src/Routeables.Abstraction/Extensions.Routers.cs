using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Routeables.Abstraction;

/// <summary>Routers by region, route changes and modals on <see cref="IServiceProvider"/> and <see cref="IRouter"/>.</summary>
public static partial class Extensions
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
    public static ILockCoordinator? CreateLockCoordinator(this IRouter router, IMainViewModel mainViewModel)
        => (router as ILockCoordinatorFactory)?.CreateLockCoordinator(mainViewModel);

    /// <summary>The router that shows <paramref name="view"/>: the one of its <see cref="IRouteableEntry.Region"/>.</summary>
    public static IRouter RouterFor(this IServiceProvider provider, IRouteableEntry view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return provider.GetRouter(view.Region);
    }

    /// <summary>
    /// Shows <paramref name="view"/> in its region (the default router if it names none). A <c>RouteableRegion</c> given the
    /// same name displays it: the call does not need to know where on screen that is.
    /// </summary>
    public static Task<RouteableResult> GoToAsync(
        this IServiceProvider provider, IRouteableEntry view, CancellationToken cancellationToken = default)
        => provider.RouterFor(view).GoToRouteAsync(view, cancellationToken);

    /// <summary>
    /// Registers <typeparamref name="T"/> as a DI-scoped service, once, if no registration for it
    /// exists yet. Use this for state that must not leak between circuits/scopes
    /// (e.g. <see cref="ScopedViewCache"/>).
    /// </summary>
    static void EnsureScoped<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(this IServiceCollection services) where T : class
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
}

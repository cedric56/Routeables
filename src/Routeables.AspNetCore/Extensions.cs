using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Core;
using Routeables.Routing;
using Routeables.Segments;
using Routeables.Services;

namespace Routeables.AspNetCore;

/// <summary>
/// Extension methods that wire Routeables into the ASP.NET Core dependency-injection container.
/// </summary>
public static class Extensions
{
    /// <summary>
    /// <c>AddRouteablesAspNetCore&lt;TMain&gt;</c> for an application that has no menu: no main view model to write.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Optional callback to register additional services after the defaults.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddRouteablesAspNetCore(
        this IServiceCollection services,
        Action<IServiceCollection>? configure = null)
        => services.AddRouteablesAspNetCore<DefaultMainViewModel>(configure);

    /// <summary>
    /// Registers the core Routeables services for ASP.NET Core, using <typeparamref name="TMain"/> as the main view-model.
    /// </summary>
    /// <typeparam name="TMain">The main view-model type. Must implement <see cref="IMainViewModel"/>.</typeparam>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Optional callback to register additional services after the defaults.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection
        AddRouteablesAspNetCore<TMain>(
            this IServiceCollection services,
            Action<IServiceCollection>? configure = null)
        where TMain : class, IMainViewModel
    {
        services.RegisterMainViewModel<TMain>(ServiceLifetime.Scoped);
        services.AddRouteCoordinator(ServiceLifetime.Scoped);
        services.AddScoped<RouteableInteractions>();
        services.RegisterRouteableArgsFactory(() => RouteableArgs.Create());
        services.AddScoped<IRouteableDialogService, RouteableDialogService>();
        services.AddScoped<IRouteableNotificationService, RouteableNotificationService>();
        services.AddScoped<RouteableRequestThemeService>();
        services.AddScoped<IRouteableRequestedThemeService>(p => p.GetRequiredService<RouteableRequestThemeService>());
        services.AddScoped(
            _ => new RouteableHostLifetime(
                ServiceLifetime.Scoped));
        services.RegisterRouting(ServiceLifetime.Scoped);
        services.AddScoped<RouteablePageState>();
        configure?.Invoke(services);

        return services;
    }
}
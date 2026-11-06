using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Routing;
using Routeables.Segments;
using Routeables.Services;

namespace Routeables.AspNetCore;

public static class Extensions
{
    public static IServiceCollection
        AddRouteablesAspNetCore<TMain>(
            this IServiceCollection services,
            Action<IServiceCollection>? configure = null)
        where TMain : class, IMainViewModel
    {
        services.RegisterMainViewModel<TMain>(ServiceLifetime.Scoped);
        services.AddRouteCoordinator(ServiceLifetime.Scoped);
        services.AddScoped<IRouteableRequestedThemeService>(_ => null!);
        services.AddScoped(
            _ => new RouteableHostLifetime(
                ServiceLifetime.Scoped));
        services.RegisterRouting(ServiceLifetime.Scoped);

        configure?.Invoke(services);

        return services;
    }
}

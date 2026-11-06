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

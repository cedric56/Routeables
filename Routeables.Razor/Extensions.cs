using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Core;
using Routeables.Routing;
using Routeables.Services;

namespace Routeables.Razor;

/// <summary>
/// Registers Routeables in a Blazor application's <see cref="IServiceCollection"/>.
/// </summary>
public static class Extensions
{
    /// <summary>
    /// Registers the router, the property injection, the address synchronization, the environment information and the theme.
    /// The dialogs, content dialogs, task dialogs, modals and notifications are optional: add
    /// <c>UseRouteablesServices()</c> from <c>Routeables.Razor.Services</c> to get them.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="runtime">The runtime environment settings.</param>
    /// <param name="onCircuitProviderChanged">Optional callback executed when the circuit provider initializes.</param>
    public static IServiceCollection UseRouteables(this IServiceCollection services,
        Runtime runtime,
        Func<IServiceProvider, Task>? onCircuitProviderChanged = null)
    {
        services.AddScoped<RouteableUrlSync>();
        services.RegisterRouteableArgsFactory(() => RouteableArgs.Create());

        services.AddSingleton<IInjection>(
            new PropertyInjection(
                new RouteablePropertyEntry(typeof(InjectAttribute)),
                new RouteablePropertyEntry(typeof(ParameterAttribute), FromContainer: false)));

        services.AddSingleton<Func<IServiceProvider, Task>>(onCircuitProviderChanged ?? (_ => Task.CompletedTask));
        services.RegisterRouting(ServiceLifetime.Scoped);
        services.RegisterEnvironments(runtime);
        services.AddScoped<IRouteableRequestedThemeService, RouteableRequestThemeService>();
        return services;
    }
}

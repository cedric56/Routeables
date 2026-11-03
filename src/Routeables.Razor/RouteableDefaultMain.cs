using Microsoft.Extensions.DependencyInjection;
using Routeables.Core;

namespace Routeables.Razor;

/// <summary><c>UseRouteables...</c> for an application that has no menu: <see cref="RouteableDefaultMainViewModel"/> is its main view model.</summary>
public static class RouteableDefaultMain
{
    /// <summary>
    /// Starts Routeables in a Blazor Server application without a main view model of your own. Say which page opens first with
    /// <c>AddHome("/route")</c> in <paramref name="configure"/>.
    /// <code>
    /// services.UseRouteablesServer(r => { r.AddGeneratedRoutes(); r.AddHome("/hello/World"); });
    /// </code>
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configure">Optional: your own registrations (routes, modules, services).</param>
    /// <param name="onCircuitProviderChanged">Optional callback executed when the circuit provider initializes.</param>
    public static IServiceCollection UseRouteablesServer(
        this IServiceCollection services,
        Action<IServiceCollection>? configure = null,
        Func<IServiceProvider, Task>? onCircuitProviderChanged = null)
        => services.UseRouteablesServer<RouteableDefaultMainViewModel>(configure, onCircuitProviderChanged);

    /// <summary>Starts Routeables in a Blazor WebAssembly application without a main view model of your own (see <see cref="UseRouteablesServer(IServiceCollection, Action{IServiceCollection}?, Func{IServiceProvider, Task}?)"/>).</summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configure">Optional: your own registrations (routes, modules, services).</param>
    public static IServiceCollection UseRouteablesWebAssembly(this IServiceCollection services, Action<IServiceCollection>? configure = null)
        => services.UseRouteablesWebAssembly<RouteableDefaultMainViewModel>(configure);

    /// <summary>Starts Routeables in a MAUI Blazor Hybrid application without a main view model of your own (see <see cref="UseRouteablesServer(IServiceCollection, Action{IServiceCollection}?, Func{IServiceProvider, Task}?)"/>).</summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configure">Optional: your own registrations (routes, modules, services).</param>
    public static IServiceCollection UseRouteablesHybrid(this IServiceCollection services, Action<IServiceCollection>? configure = null)
        => services.UseRouteablesHybrid<RouteableDefaultMainViewModel>(configure);
}

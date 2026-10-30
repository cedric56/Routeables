using Microsoft.AspNetCore.Components;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Routeables.Abstraction;
using Routeables.Core;
using Routeables.Razor.Services;
using Routeables.Routing;
using Routeables.Services;

namespace Routeables.Razor;

/// <summary>
/// Registers Routeables in a Blazor application's <see cref="IServiceCollection"/>.
/// </summary>
public static class Extensions
{
    /// <summary>
    /// Starts Routeables in a Blazor Server application: registers the router, the property injection, the address synchronization,
    /// the environment information and the theme, and <typeparamref name="TMain"/> as the main view model (the menu), scoped: one per circuit.
    /// It registers the dialogs (message boxes, content dialogs and task dialogs), modals and notifications too (<c>UseRouteablesServices()</c> of
    /// <c>Routeables.Razor.Services</c>); call that one again only to replace a service or to move the notifications.
    /// <code>
    /// services.UseRouteablesServer&lt;MainViewModel&gt;(r => r.AddGeneratedRoutes());
    /// </code>
    /// </summary>
    /// <typeparam name="TMain">The main view model.</typeparam>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configure">Optional: your own registrations (routes, modules, services), the same block as on the other hosts.</param>
    /// <param name="onCircuitProviderChanged">Optional callback executed when the circuit provider initializes.</param>
    public static IServiceCollection UseRouteablesServer<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TMain>(this IServiceCollection services,
        Action<IServiceCollection>? configure = null,
        Func<IServiceProvider, Task>? onCircuitProviderChanged = null)
        where TMain : class, IMainViewModel
        => services.UseRouteablesCore<TMain>(Runtime.BlazorServer, configure, onCircuitProviderChanged);

    /// <summary>
    /// Starts Routeables in a Blazor WebAssembly application: the same as <see cref="UseRouteablesServer{TMain}"/>, for the browser.
    /// <code>
    /// builder.Services.UseRouteablesWebAssembly&lt;MainViewModel&gt;(r => r.AddGeneratedRoutes());
    /// </code>
    /// </summary>
    /// <typeparam name="TMain">The main view model.</typeparam>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configure">Optional: your own registrations (routes, modules, services), the same block as on the other hosts.</param>
    public static IServiceCollection UseRouteablesWebAssembly<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TMain>(this IServiceCollection services,
        Action<IServiceCollection>? configure = null)
        where TMain : class, IMainViewModel
        => services.UseRouteablesCore<TMain>(Runtime.BlazorWebAssembly, configure, null);

    /// <summary>
    /// Starts Routeables in a MAUI Blazor Hybrid application: the same as <see cref="UseRouteablesServer{TMain}"/>, in a web view.
    /// <code>
    /// builder.Services.UseRouteablesHybrid&lt;MainViewModel&gt;(r => r.AddGeneratedRoutes());
    /// </code>
    /// </summary>
    /// <typeparam name="TMain">The main view model.</typeparam>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configure">Optional: your own registrations (routes, modules, services), the same block as on the other hosts.</param>
    public static IServiceCollection UseRouteablesHybrid<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TMain>(this IServiceCollection services,
        Action<IServiceCollection>? configure = null)
        where TMain : class, IMainViewModel
        => services.UseRouteablesCore<TMain>(Runtime.MauiHybrid, configure, null);

    private static IServiceCollection UseRouteablesCore<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TMain>(this IServiceCollection services,
        Runtime runtime,
        Action<IServiceCollection>? configure,
        Func<IServiceProvider, Task>? onCircuitProviderChanged)
        where TMain : class, IMainViewModel
    {
        ArgumentNullException.ThrowIfNull(services);
        if (runtime == Runtime.BlazorServer
            && services.Any(d => d.ServiceType == typeof(IMainViewModel) && d.Lifetime != ServiceLifetime.Scoped))
            throw new InvalidOperationException(
                "RegisterMainViewModel ran before UseRouteablesServer and registered the main view model as a singleton, which Blazor Server shares between circuits. "
                + "Remove it: UseRouteablesServer<TMain>() registers the main view model.");
        services.TryAddSingleton(new RouteableHostLifetime(ServiceLifetime.Scoped));

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
        services.AddScoped<IRouteableDispatcher, RouteableDispatcher>();
        services.RegisterMainViewModel<TMain>();
        services.UseRouteablesServices();
        configure?.Invoke(services);
        return services;
    }
}

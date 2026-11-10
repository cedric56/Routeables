using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Injections;
using Routeables.Abstraction.Lifecycle;
using Routeables.Abstraction.Routing;
using Routeables.Core;
using Routeables.Routing;
using Routeables.Segments;
using Routeables.Services;

namespace Routeables.AspNetCore;

/// <summary>
/// Extension methods that wire Routeables into the ASP.NET Core dependency-injection container.
/// </summary>
public static class AspNetCoreRouteablesExtensions
{
    /// <summary>
    /// <c>AddRouteablesAspNetCore&lt;TMenu&gt;</c> for an application that has no menu: no menu view model to write.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Optional callback to register additional services after the defaults.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddRouteablesAspNetCore(
        this IServiceCollection services,
        Action<IServiceCollection>? configure = null)
        => services.AddRouteablesAspNetCore<DefaultRouteableMenuViewModel>(configure);

    /// <summary>
    /// Registers the core Routeables services for ASP.NET Core, using <typeparamref name="TMenu"/> as the menu view-model.
    /// </summary>
    /// <typeparam name="TMenu">The menu view-model type. Must implement <see cref="IRouteableMenuViewModel"/>.</typeparam>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Optional callback to register additional services after the defaults.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection
        AddRouteablesAspNetCore<TMenu>(
            this IServiceCollection services,
            Action<IServiceCollection>? configure = null)
        where TMenu : class, IRouteableMenuViewModel
    {
        services.RegisterEnvironments(Runtime.AspNetCore);
        services.RegisterMenuViewModel<TMenu>(ServiceLifetime.Scoped);
        services.AddRouteCoordinator(ServiceLifetime.Scoped);
        services.AddScoped<RouteableInteractions>();
        services.RegisterRouteableArgsFactory(() => RouteableArgs.Create());
        // A template parameter written {name::} goes to a property marked [RouteableParameter] (a Razor view is built by MVC and has none of its own,
        // so it is the property of the view model that receives it).
        services.AddSingleton<IRouteableInjection>(
            new RouteablePropertyInjection(new RouteablePropertyEntry(typeof(RouteableParameterAttribute), FromContainer: false)));
        services.AddScoped<IRouteableDialogService, RouteableDialogService>();
        services.AddScoped<IRouteableNotificationService, RouteableNotificationService>();
        services.AddScoped<RouteableRequestThemeService>();
        services.AddScoped<IRouteableRequestedThemeService>(p => p.GetRequiredService<RouteableRequestThemeService>());
        // An instance, as Blazor registers it: GetRouteableLifetime() reads the collection and does not see a factory.
        services.TryAddSingleton(new RouteableHostLifetime(ServiceLifetime.Scoped));
        services.RegisterRouting(ServiceLifetime.Scoped);
        services.AddScoped<RouteablePageState>();
        services.AddScoped<RouteableTransitions>();
        configure?.Invoke(services);
        // The routes the generator found are added unless the application registered them (or called AddGeneratedRoutes()).
        global::Routeables.Abstraction.Registrations.RouteableAutoRegistration.Apply(services);

        return services;
    }
}
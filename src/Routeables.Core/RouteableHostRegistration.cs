using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Routeables.Abstraction;
using Routeables.Routing;

namespace Routeables.Core;

/// <summary>What every desktop and mobile host registers the same way: the router, the arguments factory, the property injection and the environment.</summary>
internal static class RouteableHostRegistration
{
    /// <summary>
    /// Registers the router, the arguments factory, the runtime information and the property injection. A property is
    /// injected from a route change value when it carries <see cref="RouteableParameterAttribute"/> or one of
    /// <paramref name="otherProperties"/> (the native attribute of the host).
    /// </summary>
    /// <param name="services">The service collection of the host.</param>
    /// <param name="runtime">The runtime the host runs.</param>
    /// <param name="otherProperties">The other attributes the host reads, after <see cref="RouteableParameterAttribute"/>.</param>
    public static void AddRouteableHost(this IServiceCollection services, Runtime runtime, params RouteablePropertyEntry[] otherProperties)
    {
        services.RegisterRouteableArgsFactory(() => RouteableArgs.Create());
        services.AddSingleton<IInjection>(
            new PropertyInjection([new RouteablePropertyEntry(typeof(RouteableParameterAttribute), FromContainer: false), .. otherProperties]));
        services.RegisterEnvironments(runtime);
        services.RegisterRouting();
        services.TryAddSingleton(new RouteableHostLifetime(ServiceLifetime.Singleton));
    }

    /// <summary>
    /// Builds the container. While developing, a registration that cannot be built (a missing dependency of a view or view
    /// model) fails at start with its name, instead of at the first route change to it. Scopes are not validated: the hosts
    /// resolve everything from the root provider.
    /// </summary>
    public static IServiceProvider BuildRouteableProvider(this IServiceCollection services)
    {
#if DEBUG
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
#else
        return services.BuildServiceProvider();
#endif
    }
}

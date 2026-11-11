using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Routeables.Abstraction;
using Routeables.Abstraction.Injections;
using Routeables.Abstraction.Lifecycle;
using Routeables.Abstraction.Routing;
using Routeables.Routing;
using Routeables.Services;

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
        services.AddSingleton<IRouteableInjection>(
            new RouteablePropertyInjection([new RouteablePropertyEntry(typeof(RouteableParameterAttribute), FromContainer: false), .. otherProperties]));
        services.RegisterEnvironments(runtime);
        services.RegisterRouting();
        services.TryAddSingleton(new RouteableHostLifetime(ServiceLifetime.Singleton));
        services.TryAddSingleton<RouteableAppLifecycle>();
    }

    /// <summary>
    /// Builds the container. While developing, a registration that cannot be built (a missing dependency of a view or view
    /// model) fails at start with its name, instead of at the first route change to it. Scopes are not validated: the hosts
    /// resolve everything from the root provider. An application that chose its own container (<see cref="RouteableContainer.UseRouteablesContainer{TContainerBuilder}"/>)
    /// gets that one, built the way its library builds it.
    /// </summary>
    public static IServiceProvider BuildRouteableProvider(this IServiceCollection services)
    {
        var own = RouteableContainer.Find(services);
        if (own is not null)
            return own(services).StartRouteableHistory();

        // A debug build of the library validates always; the package (a release build) validates while a debugger is attached, which is
        // when the application is being developed: the "#if DEBUG" alone would never run for a user of the package.
#if DEBUG
        var validate = true;
#else
        var validate = System.Diagnostics.Debugger.IsAttached;
#endif
        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = validate });
        // The history of the previous run is put back, when the application asked for it (AddRouteableHistory).
        return provider.StartRouteableHistory();
    }
}

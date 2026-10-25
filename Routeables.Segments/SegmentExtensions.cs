using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Routeables.Abstraction;
using Routeables.Routing;

namespace Routeables.Segments;

/// <summary>
/// Registration of the segment coordinator: <see cref="IRouteCoordinator"/> and the segment names it answers to.
/// </summary>
public static class SegmentExtensions
{
    /// <summary>
    /// Registers <see cref="IRouteCoordinator"/>, which runs segment route changes (<c>Orders?id=5/Detail</c>) over the
    /// registered routers. Call it once at startup, after <c>RegisterRouting</c> (or after <c>AddRegion</c> when the
    /// application registers regions only): routing alone does not register it.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="lifetime">
    /// The lifetime of the routers it drives, which is the one of the host when omitted: a coordinator resolves region routers
    /// from its own provider. Transient is not supported (the coordinator exposes events).
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lifetime"/> is <see cref="ServiceLifetime.Transient"/>.</exception>
    public static IServiceCollection AddRouteCoordinator(
        this IServiceCollection services, ServiceLifetime? lifetime = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        var chosen = lifetime ?? services.GetRouteableLifetime();
        if (chosen == ServiceLifetime.Transient)
            throw new ArgumentOutOfRangeException(nameof(lifetime), chosen, "A transient coordinator would lose its subscribers.");

        var registry = GetSegmentRegistry(services);
        services.TryAdd(new ServiceDescriptor(
            typeof(IRouteCoordinator), sp => new RouteCoordinator(sp, registry), chosen));
        return services;
    }

    /// <summary>
    /// Makes <typeparamref name="TViewModel"/> reachable as a route change segment. The default name is the type name:
    /// <c>RegisterSegment&lt;OrdersViewModel&gt;()</c> answers to <c>OrdersViewModel?id=5</c>. The view model must be
    /// registered with <c>RegisterRoute</c> (without a route template: segments use the default keys, or a literal
    /// <paramref name="route"/> that matches itself).
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="name">The segment name; the view model type name when omitted. Case-insensitive.</param>
    /// <param name="route">The route key the view model was registered under, if it was not the default one.</param>
    /// <param name="template">The matching template, if it differs from <paramref name="route"/> (which is used when it is omitted).</param>
    /// <exception cref="InvalidOperationException">A segment with the same name is already registered.</exception>
    public static IServiceCollection RegisterSegment<TViewModel>(
        this IServiceCollection services, string? name = null, string? route = null, string? template = null)
        where TViewModel : class
    {
        ArgumentNullException.ThrowIfNull(services);
        var segment = string.IsNullOrWhiteSpace(name) ? typeof(TViewModel).Name : name;
        GetSegmentRegistry(services).Add(segment, () => new RouteableEntry<TViewModel>(segment, null, route, template ?? route));
        return services;
    }

    // One registry per collection, found through its instance registration so the order of calls does not matter.
    private static SegmentRegistry GetSegmentRegistry(IServiceCollection services)
    {
        var registry = services.Select(d => d.ImplementationInstance).OfType<SegmentRegistry>().FirstOrDefault();
        if (registry is null)
        {
            registry = new SegmentRegistry();
            services.AddSingleton(registry);
        }
        return registry;
    }
}

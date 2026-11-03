using System.Diagnostics.CodeAnalysis;
using Routeables.Abstraction;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Routeables.Routing;

/// <summary>
/// Registration and resolution helpers for the Routeables routing system: route/view/view-model
/// registration on <see cref="IServiceCollection"/>, and view/view-model/modal resolution
/// on <see cref="IServiceProvider"/>.
/// </summary>
public static class Extensions
{
    /// <summary>
    /// Registers the default (unkeyed) routing stack: <see cref="IRouter"/> plus its
    /// history, gate, and lifecycle helpers, the broker service and the lock-coordinator factory.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="lifetime">
    /// Defaults to <see cref="ServiceLifetime.Singleton"/> so all injectors share one history
    /// (desktop / MAUI / Avalonia). Prefer <see cref="ServiceLifetime.Scoped"/> on Blazor
    /// Server so each circuit gets its own stack, or use <see cref="AddRegion"/>
    /// when you need multiple independent panes.
    /// Do not use <see cref="ServiceLifetime.Transient"/>: each resolve would get a separate
    /// history and routing would appear broken across view-models.
    /// </param>
    /// <param name="getRouter">Optional factory to supply a custom <see cref="IRouter"/> instead of the default <see cref="Router"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lifetime"/> is <see cref="ServiceLifetime.Transient"/>.</exception>
    public static void RegisterRouting(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Singleton,
        Func<IServiceProvider, IRouter>? getRouter = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (lifetime == ServiceLifetime.Transient)
            throw new ArgumentOutOfRangeException(
                nameof(lifetime),
                lifetime,
                "A Transient router is not supported: each resolve would get a separate history use Regions instead.");

        // Router collaborators share the router's lifetime so they live and die together.
        services.Add(new ServiceDescriptor(typeof(RouterHistory), sp => new RouterHistory(sp.GetService<HistoryLimits>()?.For(null)), lifetime));
        services.Add(new ServiceDescriptor(typeof(RouterGate), _ => new RouterGate(), lifetime));
        services.Add(new ServiceDescriptor(typeof(RouterLifecycle), sp => new RouterLifecycle(sp), lifetime));
        services.Add(new ServiceDescriptor(typeof(IRouter), sp =>
            getRouter is not null ?
                getRouter(sp) :
                new Router(
                    sp.GetRequiredService<RouterHistory>(),
                    sp.GetRequiredService<RouterGate>(),
                    sp.GetRequiredService<RouterLifecycle>(),
                    sp),
            lifetime));
    }

    /// <summary>
    /// Registers an independent route change region: a <see cref="Router"/> resolved as a keyed
    /// service under <paramref name="route"/>. Use this instead of registering <see cref="Router"/>
    /// as a plain, unkeyed singleton whenever a view model needs more than one independent
    /// routing area (e.g. a main pane and a side pane) — resolving an unkeyed singleton twice
    /// returns the *same* instance, silently merging what were meant to be two separate
    /// routing histories.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="route">The region's identifier, e.g. "main" or "side". Resolve it later with <see cref="Abstraction.Extensions.GetRouter"/></param>
    /// <param name="lifetime"><see cref="ServiceLifetime.Scoped"/></param>
    /// <returns>The same <paramref name="services"/> instance, for chaining.</returns>
    public static IServiceCollection AddRegion(
        this IServiceCollection services, string route, ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(route);
        services.Add(new ServiceDescriptor(typeof(RouterHistory), route, (sp, k) => new RouterHistory(sp.GetService<HistoryLimits>()?.For((string)k!)), lifetime));
        services.Add(new ServiceDescriptor(typeof(RouterGate), route, (sp, k) => new RouterGate(), lifetime));
        services.Add(new ServiceDescriptor(typeof(RouterLifecycle), route, (sp, k) => new RouterLifecycle(sp), lifetime));
        services.Add(new ServiceDescriptor(typeof(IRouter), route, (sp, k) =>
            new Router(
                sp.GetRequiredKeyedService<RouterHistory>(k),
                sp.GetRequiredKeyedService<RouterGate>(k),
                sp.GetRequiredKeyedService<RouterLifecycle>(k),
                sp),
            lifetime));

        return services;
    }

    /// <summary>
    /// Limits the history of a router: when a push goes beyond <paramref name="maxSize"/> entries, the oldest entry is
    /// dropped (and destroyed like a popped one). Without this call a history has no limit. Call it at startup, before or
    /// after <see cref="RegisterRouting"/> / <see cref="AddRegion"/>.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="maxSize">The most entries kept, at least 1.</param>
    /// <param name="region">The region whose history is limited; the default router when <see langword="null"/>.</param>
    /// <returns>The same <paramref name="services"/> instance, for chaining.</returns>
    public static IServiceCollection LimitHistory(this IServiceCollection services, int maxSize, string? region = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxSize, 1);
        var limits = (HistoryLimits?)services.FirstOrDefault(d => d.ServiceType == typeof(HistoryLimits))?.ImplementationInstance;
        if (limits is null)
        {
            limits = new HistoryLimits();
            services.AddSingleton(limits);
        }
        limits.Set(region, maxSize);
        return services;
    }
}
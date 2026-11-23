using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Routeables.Abstraction;
using Routeables.Abstraction.Errors;
using Routeables.Abstraction.Routing;

namespace Routeables.Services;

/// <summary>Registration of <see cref="RouteableHistoryKeeper"/>.</summary>
public static class RouteableHistoryExtensions
{
    /// <summary>
    /// Keeps the history of the router between two runs of the application (see <see cref="RouteableHistoryKeeper"/>). Call it with the other
    /// registrations of the application; the host starts it when it builds its container.
    /// </summary>
    /// <param name="services">The service collection of the application.</param>
    /// <param name="path">The file that holds the history; <see langword="null"/> takes <see cref="FileRouteableHistoryStore"/>'s default.</param>
    public static IServiceCollection AddRouteableHistory(this IServiceCollection services, string? path = null)
        => services.AddRouteableHistory(path, protector: null);

    /// <summary>
    /// Keeps the history of the router between two runs of the application, encrypted by <paramref name="protector"/>
    /// (see <see cref="AesRouteableHistoryProtector"/>).
    /// </summary>
    /// <remarks>Call it once: like the other <c>TryAdd</c> registrations, a second call (with another path or protector) changes nothing, the first one stays.</remarks>
    /// <param name="services">The service collection of the application.</param>
    /// <param name="path">The file that holds the history; <see langword="null"/> takes <see cref="FileRouteableHistoryStore"/>'s default.</param>
    /// <param name="protector">Encrypts what is kept; <see langword="null"/> keeps plain text, which is only fit for histories that hold no private data.</param>
    public static IServiceCollection AddRouteableHistory(this IServiceCollection services, string? path, IRouteableHistoryProtector? protector)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (protector is not null)
            services.TryAddSingleton(protector);
        services.TryAddSingleton<IRouteableHistoryStore>(_ => new FileRouteableHistoryStore(path));
        services.TryAddSingleton(sp => new RouteableHistoryKeeper(sp, sp.GetRequiredService<IRouteableHistoryStore>(), sp.GetService<IRouteableHistoryProtector>()));
        return services;
    }

    /// <summary>Starts the keeper when <see cref="AddRouteableHistory(IServiceCollection, string?)"/> registered it; does nothing otherwise. A host calls it once its container is built.</summary>
    public static IServiceProvider StartRouteableHistory(this IServiceProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        provider.GetService<RouteableHistoryKeeper>()?.Start();
        return provider;
    }
}

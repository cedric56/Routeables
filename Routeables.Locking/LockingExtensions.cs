using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Routeables.Abstraction;

namespace Routeables.Locking;

/// <summary>
/// Registration of the locking of the main entries: a page that blocks leaving locks the menu while it is shown.
/// </summary>
public static class LockingExtensions
{
    /// <summary>
    /// Registers the broker (<see cref="IRouteableBrokerService"/>) and the coordinator that listens to it. A main view model
    /// that derives from <c>MainViewModelBase</c> then locks and unlocks its entries when a
    /// <see cref="RouteableLockMessage"/> is published. Without this call nothing is locked and no broker exists.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    public static IServiceCollection AddLocking(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // TryAdd: an application-provided broker registration takes precedence. Scoped, like the router it serves.
        services.TryAdd(new ServiceDescriptor(typeof(IRouteableBrokerService), typeof(RouteableBrokerService), ServiceLifetime.Scoped));
        services.RegisterLockCoordinator<LockCoordinator>();
        return services;
    }
}

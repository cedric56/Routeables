using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Routeables.Abstraction;
using Routeables.Abstraction.Locking;

namespace Routeables.Locking;

/// <summary>
/// Registration of the locking of the main entries: a page that blocks leaving locks the menu while it is shown.
/// </summary>
public static class RouteableLockingExtensions
{
    /// <summary>
    /// Registers the broker (<see cref="IRouteableLockBroker"/>), the coordinator that listens to it and the lock of forms (<see cref="IRouteableFormLock"/>). A main view model
    /// that derives from <c>MainViewModelBase</c> then locks and unlocks its entries when a
    /// <see cref="RouteableLockMessage"/> is published. Without this call nothing is locked and no broker exists.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    public static IServiceCollection AddLocking(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // TryAdd: an application-provided broker registration takes precedence. Scoped, like the router it serves.
        services.TryAdd(new ServiceDescriptor(typeof(IRouteableLockBroker), typeof(RouteableLockBroker), ServiceLifetime.Scoped));
        services.RegisterLockCoordinator<RouteableLockCoordinator>();
        // The lock of a form: view models ask for it through the contract of Routeables.Abstraction. Scoped, like the broker it publishes on.
        services.TryAddScoped<IRouteableFormLock, RouteableFormLock>();
        return services;
    }
}

using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Core;
using Routeables.Locking;
using Routeables.Services;

namespace Routeables.Tests.Hosts;

/// <summary>
/// The assertions every host owes the application. One copy, run by <see cref="HostContractTests"/> for Blazor, Avalonia, Uno and
/// MAUI and by the WPF tests (compiled on Windows only) for WPF: a host cannot drift by re-writing them.
/// </summary>
public static class HostContract
{
    public static void ProvidesRouterAndInjection(IServiceProvider provider)
    {
        Assert.NotNull(provider.GetRequiredService<IRouter>());
        Assert.NotNull(provider.GetRequiredService<IInjection>());
        // A view model can reach the thread of the interface on every host.
        Assert.NotNull(provider.GetRequiredService<IRouteableDispatcher>());
        // Locking is optional: no host registers the broker or the lock coordinator (AddLocking does).
        Assert.Null(provider.GetService<IRouteableBrokerService>());
    }

    public static void MainViewModelIsOneInstance(IServiceProvider provider)
        => Assert.Same(provider.GetRequiredService<IMainRouteables>(), provider.GetRequiredService<IMainRouteables>());

    public static void ValueAttributesNeverComeFromTheContainer(IServiceProvider provider)
    {
        var valueAttributes = new[] { "ParameterAttribute", "RouteableParameterAttribute", "QueryPropertyAttribute" };

        foreach (var entry in provider.GetRequiredService<IInjection>().AttributeTypes)
            if (valueAttributes.Contains(entry.Type.Name))
                Assert.True(!entry.FromContainer);
    }
}

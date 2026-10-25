using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Routeables.Abstraction;
using Routeables.Routing;

namespace Routeables.Tests.Routing;

public class RoutingRegistrationTests
{
    [Fact]
    public void RegisterRouting_rejects_null_services()
    {
        Assert.Throws<ArgumentNullException>(() => Routeables.Routing.Extensions.RegisterRouting(null!));
    }

    [Fact]
    public void RegisterRouting_uses_custom_router_factory_once_for_singleton_lifetime()
    {
        var services = new ServiceCollection();
        var router = Substitute.For<IRouter>();
        IServiceProvider? factoryProvider = null;
        var factoryCalls = 0;
        services.RegisterRouting(getRouter: provider =>
        {
            factoryCalls++;
            factoryProvider = provider;
            return router;
        });
        using var provider = services.BuildServiceProvider();

        var first = provider.GetRequiredService<IRouter>();
        var second = provider.GetRequiredService<IRouter>();

        Assert.Same(router, first);
        Assert.Same(first, second);
        Assert.NotNull(factoryProvider);
        Assert.Same(first, factoryProvider!.GetRequiredService<IRouter>());
        Assert.Equal(1, factoryCalls);
    }

    [Fact]
    public void AddRegion_rejects_null_services_and_blank_route()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() => Routeables.Routing.Extensions.AddRegion(null!, "main"));
        Assert.Throws<ArgumentException>(() => services.AddRegion(""));
        Assert.Throws<ArgumentException>(() => services.AddRegion("  "));
    }

    [Fact]
    public void AddRegion_returns_the_collection_and_registers_independent_singletons_per_key()
    {
        var services = new ServiceCollection();
        Assert.Same(services, services.AddRegion("main", ServiceLifetime.Singleton));
        services.AddRegion("side", ServiceLifetime.Singleton);
        using var provider = services.BuildServiceProvider();

        var main = provider.GetRequiredKeyedService<IRouter>("main");
        var mainAgain = provider.GetRequiredKeyedService<IRouter>("main");
        var side = provider.GetRequiredKeyedService<IRouter>("side");

        Assert.Same(main, mainAgain);
        Assert.NotSame(main, side);
        Assert.NotSame(main.History, side.History);
        Assert.Same(main, provider.GetRouter("main"));
    }
}

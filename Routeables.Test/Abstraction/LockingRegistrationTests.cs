using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Locking;
using Routeables.Routing;
using Routeables.Tests.Core;

namespace Routeables.Tests.Abstraction;

public class LockingRegistrationTests
{
    private static Router CreateRouter(IServiceCollection services)
        => new(new RouterHistory(), new RouterGate(), new RouterLifecycle(), services.BuildServiceProvider());

    [Fact]
    public void Without_AddLocking_the_router_offers_no_coordinator_and_there_is_no_broker()
    {
        var services = new ServiceCollection();
        var router = CreateRouter(services);

        Assert.Null(router.CreateLockCoordinator(new FakeMainRouteables()));
        Assert.Null(services.BuildServiceProvider().GetService<IRouteableBrokerService>());
    }

    [Fact]
    public void AddLocking_registers_the_broker_once_and_a_coordinator_for_the_router()
    {
        var services = new ServiceCollection();
        services.AddLocking();
        services.AddLocking();

        Assert.Equal(1, services.Count(d => d.ServiceType == typeof(IRouteableBrokerService)));
        Assert.NotNull(CreateRouter(services).CreateLockCoordinator(new FakeMainRouteables()));
    }

    [Fact]
    public void AddLocking_keeps_a_broker_the_application_registered()
    {
        var services = new ServiceCollection();
        var own = new Own();
        services.AddSingleton<IRouteableBrokerService>(own);
        services.AddLocking();

        Assert.Same(own, services.BuildServiceProvider().GetRequiredService<IRouteableBrokerService>());
    }

    [Fact]
    public void AddLocking_rejects_null_services()
        => Assert.Throws<ArgumentNullException>(() => LockingExtensions.AddLocking(null!));

    private sealed class Own : IRouteableBrokerService
    {
        public void Publish(RouteableMessage message) { }
        public IDisposable Subscribe(string topic, Action<RouteableMessage> callback) => new Sub();
        private sealed class Sub : IDisposable { public void Dispose() { } }
    }
}

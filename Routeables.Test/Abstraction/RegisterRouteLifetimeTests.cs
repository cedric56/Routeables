using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Routing;

namespace Routeables.Test.Abstraction;

public class RegisterRouteLifetimeTests
{
    private sealed class StubView : IRouteableFor
    {
        public object? Context { get; set; }
    }

    private sealed class StubVm;

    [Fact]
    public void RegisterRoute_default_shares_same_IRouter_instance()
    {
        var services = new ServiceCollection();
        services.RegisterRouting();
        var sp = services.BuildServiceProvider();

        var a = sp.GetRequiredService<IRouter>();
        var b = sp.GetRequiredService<IRouter>();

        Assert.Same(a, b);
    }

    [Fact]
    public void RegisterRoute_scoped_shares_within_scope_only()
    {
        var services = new ServiceCollection();
        services.RegisterRouting(ServiceLifetime.Scoped);
        var sp = services.BuildServiceProvider();

        IRouter first;
        using (var scope1 = sp.CreateScope())
        {
            first = scope1.ServiceProvider.GetRequiredService<IRouter>();
            var again = scope1.ServiceProvider.GetRequiredService<IRouter>();
            Assert.Same(first, again);
        }

        using var scope2 = sp.CreateScope();
        var second = scope2.ServiceProvider.GetRequiredService<IRouter>();
        Assert.NotSame(first, second);
    }

    [Fact]
    public void RegisterRoute_rejects_Transient()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            services.RegisterRouting(ServiceLifetime.Transient));
    }

    [Fact]
    public async Task Shared_router_preserves_history_across_resolves()
    {
        var services = new ServiceCollection();
        services.RegisterRouting();
        var sp = services.BuildServiceProvider();

        var router1 = sp.GetRequiredService<IRouter>();
        await router1.GoAsync(new StubView(), new StubVm(), cancellationToken: TestContext.Current.CancellationToken);
        await router1.GoAsync(new StubView(), new StubVm(), cancellationToken: TestContext.Current.CancellationToken);

        var router2 = sp.GetRequiredService<IRouter>();
        Assert.Same(router1, router2);
        Assert.True(router2.CanGoBack);
        Assert.NotNull(router2.CurrentViewModel);
    }

    [Fact]
    public async Task Named_regions_have_independent_routers_and_histories()
    {
        var services = new ServiceCollection();
        services.AddRegion("main");
        services.AddRegion("side");
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var main = scope.ServiceProvider.GetRouter("main");
        var side = scope.ServiceProvider.GetRouter("side");
        await main.GoAsync(new StubView(), new StubVm(), cancellationToken: TestContext.Current.CancellationToken);
        await main.GoAsync(new StubView(), new StubVm(), cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotSame(main, side);
        Assert.Equal(2, main.History.Count);
        Assert.True(main.CanGoBack);
        Assert.Empty(side.History.Entries);
        Assert.Null(side.CurrentViewModel);
    }

    [Fact]
    public void Named_regions_are_scoped_by_default()
    {
        var services = new ServiceCollection();
        services.AddRegion("main");
        using var provider = services.BuildServiceProvider();

        IRouter first;
        using (var scope = provider.CreateScope())
        {
            first = scope.ServiceProvider.GetRouter("main");
            Assert.Same(first, scope.ServiceProvider.GetRouter("main"));
        }

        using var secondScope = provider.CreateScope();
        var second = secondScope.ServiceProvider.GetRouter("main");
        Assert.NotSame(first, second);
    }
}

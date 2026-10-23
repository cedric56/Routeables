using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Routing;
using Routeables.Tests.Core;

namespace Routeables.Test.Abstraction;

public class MainRouteablesTests
{
    private static IRouteableEntry View(string name) => new RouteableEntry("/" + name, "/" + name, name);

    [Fact]
    public void SelectFirst_selects_the_first_entry_once()
    {
        IMainRouteables main = new FakeMainRouteables { Routeables = { View("a"), View("b") } };

        Assert.True(main.SelectFirst());
        Assert.Equal("a", main.Routeable!.DisplayName);

        main.Routeable = main.Routeables[1];
        Assert.True(!main.SelectFirst());                    // something is selected: left alone
        Assert.Equal("b", main.Routeable.DisplayName);
    }

    [Fact]
    public void SelectFirst_does_nothing_without_entries()
    {
        IMainRouteables main = new FakeMainRouteables();

        Assert.True(!main.SelectFirst());
        Assert.Null(main.Routeable);
    }

    [Fact]
    public void A_scoped_or_singleton_main_is_one_instance_per_scope_or_provider()
    {
        var services = new ServiceCollection();
        services.RegisterMainRouteables<FakeMainRouteables>(ServiceLifetime.Scoped);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.Same(scope.ServiceProvider.GetRequiredService<IMainRouteables>(), scope.ServiceProvider.GetRequiredService<IMainRouteables>());
    }

    [Fact]
    public void A_transient_main_is_refused()
    {
        Assert.Throws<ArgumentException>(() => new ServiceCollection().RegisterMainRouteables<FakeMainRouteables>(ServiceLifetime.Transient));
    }
}

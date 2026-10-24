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

    // ---------------------------------------------------------------- the entries of a module

    [Fact]
    public void A_module_adds_its_entries_to_the_menu_in_the_order_it_was_added()
    {
        var services = new ServiceCollection();
        services.RegisterMainRouteables<FakeMainRouteables>(ServiceLifetime.Singleton);
        services.AddMenuEntries(() => [View("orders"), View("invoices")]);
        services.AddMenuEntries(() => [View("customers")]);
        using var provider = services.BuildServiceProvider();

        var main = provider.GetRequiredService<IMainRouteables>();

        Assert.Equal(["orders", "invoices", "customers"], main.Routeables.Select(entry => entry.DisplayName));
    }

    [Fact]
    public void The_entries_of_a_module_are_added_once_to_the_one_main_of_a_scope()
    {
        var services = new ServiceCollection();
        services.RegisterMainRouteables<FakeMainRouteables>(ServiceLifetime.Scoped);
        services.AddMenuEntries(() => [View("orders")]);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var first = scope.ServiceProvider.GetRequiredService<IMainRouteables>();
        var second = scope.ServiceProvider.GetRequiredService<IMainRouteables>();

        Assert.Same(first, second);
        Assert.Single(first.Routeables);
        Assert.Same(scope.ServiceProvider.GetRequiredService<FakeMainRouteables>(), first);   // the main itself is the same instance
    }

    [Fact]
    public void Each_scope_gets_entries_of_its_own()
    {
        var services = new ServiceCollection();
        services.RegisterMainRouteables<FakeMainRouteables>(ServiceLifetime.Scoped);
        services.AddMenuEntries(() => [View("orders")]);
        using var provider = services.BuildServiceProvider();

        using var one = provider.CreateScope();
        using var other = provider.CreateScope();
        var entryOfOne = one.ServiceProvider.GetRequiredService<IMainRouteables>().Routeables.Single();
        var entryOfOther = other.ServiceProvider.GetRequiredService<IMainRouteables>().Routeables.Single();

        entryOfOne.IsLock = true;                       // a lock in one circuit...

        Assert.NotSame(entryOfOne, entryOfOther);
        Assert.True(!entryOfOther.IsLock);              // ...does not reach the other
    }

    [Fact]
    public void Without_a_module_the_menu_is_left_as_it_is()
    {
        var services = new ServiceCollection();
        services.RegisterMainRouteables<FakeMainRouteables>(ServiceLifetime.Singleton);
        using var provider = services.BuildServiceProvider();

        Assert.Empty(provider.GetRequiredService<IMainRouteables>().Routeables);
    }

    [Fact]
    public void AddMenuEntries_refuses_no_entries()
    {
        Assert.Throws<ArgumentNullException>(() => new ServiceCollection().AddMenuEntries(null!));
    }
}

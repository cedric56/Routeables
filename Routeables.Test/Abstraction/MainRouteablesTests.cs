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
        IMainViewModel main = new FakeMainRouteables { Routeables = { View("a"), View("b") } };

        Assert.True(main.SelectFirst());
        Assert.Equal("a", main.Routeable!.DisplayName);

        main.Routeable = main.Routeables[1];
        Assert.True(!main.SelectFirst());                    // something is selected: left alone
        Assert.Equal("b", main.Routeable.DisplayName);
    }

    [Fact]
    public void SelectFirst_does_nothing_without_entries()
    {
        IMainViewModel main = new FakeMainRouteables();

        Assert.True(!main.SelectFirst());
        Assert.Null(main.Routeable);
    }

    [Fact]
    public void A_scoped_or_singleton_main_is_one_instance_per_scope_or_provider()
    {
        var services = new ServiceCollection();
        services.RegisterMainViewModel<FakeMainRouteables>(ServiceLifetime.Scoped);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.Same(scope.ServiceProvider.GetRequiredService<IMainViewModel>(), scope.ServiceProvider.GetRequiredService<IMainViewModel>());
    }

    [Fact]
    public void Without_a_lifetime_the_main_follows_the_host()
    {
        var singleton = new ServiceCollection();
        singleton.RegisterMainViewModel<FakeMainRouteables>();                       // no host: Singleton
        var scoped = new ServiceCollection();
        scoped.AddSingleton(new RouteableHostLifetime(ServiceLifetime.Scoped));     // what the Blazor host registers
        scoped.RegisterMainViewModel<FakeMainRouteables>();

        Assert.Equal(ServiceLifetime.Singleton, singleton.Last(d => d.ServiceType == typeof(IMainViewModel)).Lifetime);
        Assert.Equal(ServiceLifetime.Scoped, scoped.Last(d => d.ServiceType == typeof(IMainViewModel)).Lifetime);
    }

    [Fact]
    public void A_lifetime_given_wins_over_the_one_of_the_host()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new RouteableHostLifetime(ServiceLifetime.Scoped));
        services.RegisterMainViewModel<FakeMainRouteables>(ServiceLifetime.Singleton);

        Assert.Equal(ServiceLifetime.Singleton, services.Last(d => d.ServiceType == typeof(IMainViewModel)).Lifetime);
    }

    [Fact]
    public void A_transient_main_is_refused()
    {
        Assert.Throws<ArgumentException>(() => new ServiceCollection().RegisterMainViewModel<FakeMainRouteables>(ServiceLifetime.Transient));
    }

    // ---------------------------------------------------------------- the entries of a module

    [Fact]
    public void A_module_adds_its_entries_to_the_menu_in_the_order_it_was_added()
    {
        var services = new ServiceCollection();
        services.RegisterMainViewModel<FakeMainRouteables>(ServiceLifetime.Singleton);
        services.AddMenuEntries(() => [View("orders"), View("invoices")]);
        services.AddMenuEntries(() => [View("customers")]);
        using var provider = services.BuildServiceProvider();

        var main = provider.GetRequiredService<IMainViewModel>();

        Assert.Equal(["orders", "invoices", "customers"], main.Routeables.Select(entry => entry.DisplayName));
    }

    [Fact]
    public void A_module_can_choose_where_its_entries_go_in_the_menu()
    {
        var services = new ServiceCollection();
        services.RegisterMainViewModel<FakeMainRouteables>(ServiceLifetime.Singleton);
        services.AddMenuEntries(() => [View("first"), View("second")], index: 0);
        services.AddMenuEntries(() => [View("middle")], index: 1);
        services.AddMenuEntries(() => [View("far")], index: 99);          // past the end: last
        services.AddMenuEntries(() => [View("last")]);
        using var provider = services.BuildServiceProvider();

        var main = provider.GetRequiredService<IMainViewModel>();

        Assert.Equal(["first", "middle", "second", "far", "last"], main.Routeables.Select(entry => entry.DisplayName));
    }

    [Fact]
    public void A_module_loaded_while_running_goes_where_it_asked()
    {
        var services = new ServiceCollection();
        services.RegisterMainViewModel<FakeMainRouteables>(ServiceLifetime.Singleton);
        services.AddMenuEntries(() => [View("home"), View("about")]);
        using var provider = services.BuildServiceProvider();
        var main = provider.GetRequiredService<IMainViewModel>();

        provider.GetRequiredService<IRouteableModules>().Load("Orders", s => s.AddMenuEntries(() => [View("orders")], index: 1));

        Assert.Equal(["home", "orders", "about"], main.Routeables.Select(entry => entry.DisplayName));
    }

    [Fact]
    public void The_entries_of_a_module_are_added_once_to_the_one_main_of_a_scope()
    {
        var services = new ServiceCollection();
        services.RegisterMainViewModel<FakeMainRouteables>(ServiceLifetime.Scoped);
        services.AddMenuEntries(() => [View("orders")]);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var first = scope.ServiceProvider.GetRequiredService<IMainViewModel>();
        var second = scope.ServiceProvider.GetRequiredService<IMainViewModel>();

        Assert.Same(first, second);
        Assert.Single(first.Routeables);
        Assert.Same(scope.ServiceProvider.GetRequiredService<FakeMainRouteables>(), first);   // the main itself is the same instance
    }

    [Fact]
    public void The_menu_is_the_same_whether_the_main_or_its_interface_is_asked_for_first()
    {
        var services = new ServiceCollection();
        services.RegisterMainViewModel<FakeMainRouteables>(ServiceLifetime.Singleton);
        services.AddMenuEntries(() => [View("orders")]);
        using var provider = services.BuildServiceProvider();

        var main = provider.GetRequiredService<FakeMainRouteables>();       // the class first, not the interface

        Assert.Single(main.Routeables);
        Assert.Same(main, provider.GetRequiredService<IMainViewModel>());
        Assert.Single(main.Routeables);
    }

    [Fact]
    public void The_same_module_added_twice_adds_its_entries_once()
    {
        static IEnumerable<IRouteableEntry> Orders() => [View("orders")];

        var services = new ServiceCollection();
        services.RegisterMainViewModel<FakeMainRouteables>(ServiceLifetime.Singleton);
        services.AddMenuEntries(Orders);
        services.AddMenuEntries(Orders);
        using var provider = services.BuildServiceProvider();

        Assert.Single(provider.GetRequiredService<IMainViewModel>().Routeables);
    }

    [Fact]
    public void A_module_that_needs_another_one_calls_it_and_the_other_is_added_once()
    {
        // Orders needs Customers (a dependency is a call, as in any code); the application adds both.
        static IEnumerable<IRouteableEntry> CustomersEntries() => [View("customers")];
        static IEnumerable<IRouteableEntry> OrdersEntries() => [View("orders")];
        static IServiceCollection AddCustomersModule(IServiceCollection services) => services.AddMenuEntries(CustomersEntries);
        static IServiceCollection AddOrdersModule(IServiceCollection services) => AddCustomersModule(services).AddMenuEntries(OrdersEntries);

        var services = new ServiceCollection();
        services.RegisterMainViewModel<FakeMainRouteables>(ServiceLifetime.Singleton);
        AddOrdersModule(services);
        AddCustomersModule(services);
        using var provider = services.BuildServiceProvider();

        Assert.Equal(["customers", "orders"], provider.GetRequiredService<IMainViewModel>().Routeables.Select(entry => entry.DisplayName));
    }

    [Fact]
    public void The_container_still_checks_the_dependencies_of_the_main_when_it_is_built()
    {
        var services = new ServiceCollection();
        services.RegisterMainViewModel<MainWithDependency>(ServiceLifetime.Singleton);

        Assert.Throws<AggregateException>(() => services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true }));
    }

    private interface IMissing;

    private sealed class MainWithDependency(IMissing missing) : IMainViewModel
    {
        public IMissing Missing { get; } = missing;
        public System.Collections.ObjectModel.ObservableCollection<IRouteableEntry> Routeables { get; } = [];
        public IRouteableEntry? Routeable { get; set; }
        public object? CurrentView { get; set; }
        public EventHandler<object?>? CurrentViewChanged { get; set; }
        public EventHandler? LockChanged { get; set; }
        public string? CurrentTitle { get; }
    }

    [Fact]
    public void Each_scope_gets_entries_of_its_own()
    {
        var services = new ServiceCollection();
        services.RegisterMainViewModel<FakeMainRouteables>(ServiceLifetime.Scoped);
        services.AddMenuEntries(() => [View("orders")]);
        using var provider = services.BuildServiceProvider();

        using var one = provider.CreateScope();
        using var other = provider.CreateScope();
        var entryOfOne = one.ServiceProvider.GetRequiredService<IMainViewModel>().Routeables.Single();
        var entryOfOther = other.ServiceProvider.GetRequiredService<IMainViewModel>().Routeables.Single();

        entryOfOne.IsLock = true;                       // a lock in one circuit...

        Assert.NotSame(entryOfOne, entryOfOther);
        Assert.True(!entryOfOther.IsLock);              // ...does not reach the other
    }

    [Fact]
    public void Without_a_module_the_menu_is_left_as_it_is()
    {
        var services = new ServiceCollection();
        services.RegisterMainViewModel<FakeMainRouteables>(ServiceLifetime.Singleton);
        using var provider = services.BuildServiceProvider();

        Assert.Empty(provider.GetRequiredService<IMainViewModel>().Routeables);
    }

    [Fact]
    public void AddMenuEntries_refuses_no_entries()
    {
        Assert.Throws<ArgumentNullException>(() => new ServiceCollection().AddMenuEntries(null!));
    }
}

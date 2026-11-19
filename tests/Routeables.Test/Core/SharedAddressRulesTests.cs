using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Registrations;
using Routeables.Abstraction.Routing;
using Routeables.Core;
using Routeables.Routing;
using Routeables.Services;
using Routeables.Tests.Pages;
using System.Collections.ObjectModel;
namespace Routeables.Tests.Core;

/// <summary>
/// The menus, the router and the table of routes answer "the same route?" with the rules of <c>RouteAddress</c>: the case of a path, the slashes
/// around it and the order of a query never make two pages. The menu that the address bar asks to select an entry is any menu, not only the base class.
/// </summary>
public class SharedAddressRulesTests
{

    private sealed class Menu(IRouter router) : RouteableMenuViewModelBase(router, initialize: false)
    {
        protected override ObservableCollection<IRouteableMenuEntry> GetEntries() =>
        [
            new RouteableMenuEntry<SaProductVm>("Shoes", route: "/sa/shoes", template: "/sa/shoes"),
            new RouteableMenuEntry<SaProductVm>("Hats", route: "/sa/hats", template: "/sa/hats"),
        ];

        protected override void NotifyPropertyChanged(string? propertyName) { }
    }

    private static ServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRoute<SaProductView, SaProductVm>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/sa/shoes");
        services.RegisterRoute<SaProductView, SaProductVm>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/sa/hats");
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task The_menu_finds_the_entry_of_the_page_whatever_the_case_and_the_slashes_of_the_address_shown()
    {
        var ct = TestContext.Current.CancellationToken;
        using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();
        var menu = new Menu(router);
        Assert.True(await menu.SelectEntryAsync(menu.Entries[0], ct));

        await router.GoToAsync("/SA/Hats/", cancellationToken: ct);      // the same page as "/sa/hats", written another way
        menu.OnRouterMoved(router.CurrentViewModel);

        Assert.Equal("Hats", menu.SelectedEntry?.DisplayName);
    }

    [Fact]
    public async Task Selecting_the_entry_of_the_page_that_is_shown_is_not_a_new_route_change_even_when_written_another_way()
    {
        var ct = TestContext.Current.CancellationToken;
        using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();
        var menu = new Menu(router);
        await router.GoToAsync("/SA/SHOES", cancellationToken: ct);
        var before = router.History.Count;

        Assert.True(await menu.SelectEntryAsync(menu.Entries[0], ct));

        Assert.Equal(before, router.History.Count);
    }

    [Fact]
    public async Task The_menu_of_the_base_class_answers_through_the_interface_and_a_menu_written_by_hand_gets_the_default()
    {
        var ct = TestContext.Current.CancellationToken;
        using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();
        IRouteableMenuViewModel menu = new Menu(router);

        Assert.True(await menu.SelectEntryAsync(menu.Entries[1], ct));
        Assert.Equal("Hats", menu.SelectedEntry?.DisplayName);
        Assert.Contains("hats", router.History.Current?.Args.Route?.ToLowerInvariant());

        IRouteableMenuViewModel byHand = new FakeMenuViewModel();
        var entry = new RouteableMenuEntry<SaProductVm>("Shoes", route: "/sa/shoes", template: "/sa/shoes");
        Assert.True(await byHand.SelectEntryAsync(entry, ct));
        Assert.Same(entry, byHand.SelectedEntry);
    }

    [Fact]
    public void Two_spellings_of_one_route_are_one_entry_of_the_table()
    {
        var table = new RouteableTable();
        table.Add("/Orders", typeof(SaProductVm));
        table.Add("orders/", typeof(SaProductVm));
        table.Add("/orders?x=1", typeof(SaProductVm));      // another query is another route
        table.Add("/ORDERS/?x=1", typeof(SaProductVm));     // the same route as the one above
        table.Add("/other", typeof(SaProductVm));

        Assert.Equal(["/Orders", "/orders?x=1", "/other"], table.Entries.Select(e => e.Template).ToArray());
    }

    [Fact]
    public void A_history_keeper_that_is_disposed_twice_or_by_its_container_after_it_does_not_fail()
    {
        using var provider = Build();
        var keeper = new RouteableHistoryKeeper(provider, new MemoryStore());

        keeper.Dispose();
        keeper.Dispose();
    }

    private sealed class MemoryStore : IRouteableHistoryStore
    {
        public string? Load() => null;
        public void Save(string text) { }
    }
}

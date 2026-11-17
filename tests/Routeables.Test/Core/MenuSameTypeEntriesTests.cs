using Routeables.Abstraction;
using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Registrations;
using Routeables.Abstraction.Routing;
using Routeables.Core;
using Routeables.Routing;
using System.Collections.ObjectModel;
namespace Routeables.Tests.Core;

/// <summary>Several entries of a menu can show the same view model type with other routes: the menu follows the route, not the type.</summary>
public class MenuSameTypeEntriesTests
{
    public sealed class MsProductView : IRouteableFor { public object? Context { get; set; } }
    public sealed class MsProductVm;
    public sealed class MsOtherView : IRouteableFor { public object? Context { get; set; } }
    public sealed class MsOtherVm;

    private sealed class Menu(IRouter router) : RouteableMenuViewModelBase(router, initialize: false)
    {
        protected override ObservableCollection<IRouteableMenuEntry> GetEntries() =>
        [
            new RouteableMenuEntry<MsProductVm>("Shoes", route: "/ms/shoes", template: "/ms/shoes"),
            new RouteableMenuEntry<MsProductVm>("Hats", route: "/ms/hats", template: "/ms/hats"),
            new RouteableMenuEntry<MsOtherVm>("Other", route: "/ms-other", template: "/ms-other"),
        ];

        protected override void NotifyPropertyChanged(string? propertyName) { }
    }

    private static ServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRoute<MsProductView, MsProductVm>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/ms/shoes");
        services.RegisterRoute<MsProductView, MsProductVm>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/ms/hats");
        services.RegisterRoute<MsOtherView, MsOtherVm>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/ms-other");
        services.AddTransient<MsOtherVm>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Back_selects_the_entry_of_the_page_that_is_shown_and_not_the_first_entry_of_its_type()
    {
        var ct = TestContext.Current.CancellationToken;
        using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();
        var menu = new Menu(router);

        Assert.True(await menu.SelectEntryAsync(menu.Entries[1], ct));      // Hats
        Assert.True(await menu.SelectEntryAsync(menu.Entries[2], ct));      // Other
        await router.GoBackAsync(ct);                                       // the router goes back, not the menu: the menu follows it

        Assert.Equal("Hats", menu.SelectedEntry?.DisplayName);
    }

    [Fact]
    public async Task A_refusal_after_a_back_returns_to_the_entry_that_is_shown()
    {
        var ct = TestContext.Current.CancellationToken;
        using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();
        var menu = new Menu(router);
        await menu.SelectEntryAsync(menu.Entries[1], ct);
        await menu.SelectEntryAsync(menu.Entries[2], ct);
        await router.GoBackAsync(ct);

        // The page of "Hats" is shown: selecting it again is the same page, and the menu stays on it.
        await menu.SelectEntryAsync(menu.Entries[1], ct);

        Assert.Equal("Hats", menu.SelectedEntry?.DisplayName);
    }

    [Fact]
    public async Task Two_overlapping_back_moves_of_the_menu_are_both_followed_by_the_menu_itself()
    {
        var ct = TestContext.Current.CancellationToken;
        using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();
        var menu = new Menu(router);
        await menu.SelectEntryAsync(menu.Entries[0], ct);
        await menu.SelectEntryAsync(menu.Entries[1], ct);
        await menu.SelectEntryAsync(menu.Entries[2], ct);

        var first = menu.GoBackAsync();
        var second = menu.GoBackAsync();
        await Task.WhenAll(first, second);

        Assert.Equal("Shoes", menu.SelectedEntry?.DisplayName);
    }

    [Fact]
    public async Task Going_back_zero_pages_is_refused()
    {
        var ct = TestContext.Current.CancellationToken;
        using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => router.MoveBackAsync(0, ct));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => router.MoveBackAsync(-1, ct));
    }
}

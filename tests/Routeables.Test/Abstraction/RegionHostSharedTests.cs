using Routeables.Abstraction;
using Routeables.Abstraction.Routing;
using Routeables.Routing;
using Routeables.Regions;

namespace Routeables.Tests.Abstraction;

/// <summary>What every region host (<c>RouteableRegion</c>, <c>RouteableRegionItems</c>, <c>RouteableTabs</c>) shares, without any UI.</summary>
public class RegionHostSharedTests
{
    private sealed class Tab(string? route)
    {
        public string? Route { get; } = route;
        public object? Content { get; set; }
    }

    private static ServiceProvider Build() => RegionTestServices.Build(services => services.AddRegion("tabs"));

    [Fact]
    public void A_connection_is_active_before_its_binding_exists_and_not_after_disconnect()
    {
        using var provider = Build();
        var connection = new RegionConnection();
        var activeWhileBinding = false;
        Assert.False(connection.IsConnected);

        Assert.True(connection.Connect(provider, "tabs", router =>
        {
            activeWhileBinding = connection.Active;
            return new RegionBinding(router, _ => { });
        }));
        Assert.True(activeWhileBinding);
        Assert.True(connection.IsConnected);

        // Connected already: nothing is built twice.
        Assert.False(connection.Connect(provider, "tabs", _ => throw new InvalidOperationException()));

        connection.Disconnect();
        Assert.False(connection.Active);
        Assert.False(connection.IsConnected);
    }

    [Fact]
    public void A_connection_without_a_container_connects_nothing()
    {
        var connection = new RegionConnection();
        Assert.False(connection.Connect(null, null, _ => throw new InvalidOperationException()));
        Assert.False(connection.Active);
        Assert.False(connection.IsConnected);
    }

    [Fact]
    public void The_report_of_a_view_that_has_two_parents_names_both_and_the_region()
    {
        var error = RegionErrors.AlreadyDisplayed(new Tab("a"), new object(), "detail");
        Assert.Contains("Tab", error.Message);
        Assert.Contains("Object", error.Message);
        Assert.Contains("'detail'", error.Message);
        Assert.Contains("'(default)'", RegionErrors.AlreadyDisplayed(new Tab("a"), new object(), null).Message);
    }

    [Fact]
    public void A_subscription_runs_its_action_once()
    {
        var count = 0;
        var subscription = new RegionSubscription(() => count++);
        subscription.Dispose();
        subscription.Dispose();
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Tabs_move_the_host_route_the_selected_tab_and_follow_the_router()
    {
        using var provider = Build();
        var router = provider.GetRouter("tabs");
        var ct = TestContext.Current.CancellationToken;
        var cats = new Tab("cats"); var dogs = new Tab("dogs"); var own = new Tab("dogs") { Content = "mine" }; var plain = new Tab(null);
        Tab? selected = cats;
        var host = new object();
        var follower = new RegionTabs<Tab>(provider, "tabs", host, [cats, dogs, own, plain],
            tab => tab.Route, tab => tab.Content, (tab, content) => tab.Content = content,
            () => selected, tab => selected = tab, action => action(), typeof(RegionHostSharedTests));

        follower.Start();
        Assert.Same(host, cats.Content);
        await WaitForAsync(() => router.History.Current?.Args.Route?.Trim('/') == "cats", ct);

        // The user chooses the other tab: the host follows, the router goes to its route.
        selected = dogs;
        follower.OnSelectionChanged();
        Assert.Null(cats.Content);
        Assert.Same(host, dogs.Content);
        await WaitForAsync(() => router.History.Current?.Args.Route?.Trim('/') == "dogs", ct);

        // A tab with a content of its own, or without a route, is left alone.
        Assert.Equal("mine", own.Content);
        Assert.Null(plain.Content);

        // Back reaches the first route by another way: its tab is selected.
        await router.GoBackAsync(ct);
        Assert.Same(cats, selected);

        follower.Dispose();
        Assert.Null(cats.Content);
        Assert.Null(dogs.Content);
        Assert.Equal("mine", own.Content);
    }

    private static async Task WaitForAsync(Func<bool> condition, CancellationToken ct)
    {
        for (var i = 0; i < 200 && !condition(); i++)
            await Task.Delay(10, ct);
        Assert.True(condition());
    }
}

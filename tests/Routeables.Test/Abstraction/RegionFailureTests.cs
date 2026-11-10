using Routeables.Abstraction.Routing;
using Routeables.Regions;
using Routeables.Routing;
using Routeables.Test.Abstraction;
using static Routeables.Test.Abstraction.RouterTestKit;

namespace Routeables.Tests.Abstraction;

/// <summary>A view that cannot be shown must not be remembered as shown, and a binding that fails to start must not stay attached.</summary>
public class RegionFailureTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_failed_show_does_not_stop_the_same_view_being_shown_later()
    {
        var router = CreateRouter();
        var log = new CallLog();
        var calls = 0;
        var fail = true;
        var shown = new List<object?>();
        using var binding = new RegionBinding(router, view =>
        {
            calls++;
            if (view is not null && fail) throw new InvalidOperationException("already has a parent");
            shown.Add(view);
        });

        // The history observer that calls Refresh is isolated by the history: the route change itself succeeds.
        var page = await GoToAsync(router, "A", log);
        Assert.Equal(2, calls);                                      // the empty region at start, then the view that failed
        Assert.DoesNotContain(page.View, shown);

        fail = false;
        binding.Refresh();                                           // same view: shown now

        Assert.Equal(3, calls);
        Assert.Same(page.View, shown[^1]);
    }

    [Fact]
    public async Task A_binding_whose_first_show_fails_does_not_stay_attached_to_the_router()
    {
        var router = CreateRouter();
        var log = new CallLog();
        await GoToAsync(router, "A", log);
        var calls = 0;

        Assert.Throws<InvalidOperationException>(() => new RegionBinding(router, _ =>
        {
            calls++;
            throw new InvalidOperationException("first show fails");
        }));
        Assert.Equal(1, calls);

        await GoToAsync(router, "B", log);

        Assert.Equal(1, calls);                                      // the router no longer calls the binding that never existed
    }

    [Fact]
    public async Task The_items_binding_whose_first_show_fails_does_not_stay_attached()
    {
        var router = CreateRouter();
        var log = new CallLog();
        await GoToAsync(router, "A", log);
        var calls = 0;

        Assert.Throws<InvalidOperationException>(() => new RegionItemsBinding(router, _ =>
        {
            calls++;
            throw new InvalidOperationException("first show fails");
        }));
        await GoToAsync(router, "B", log);

        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task The_items_that_failed_to_show_are_tried_again_by_the_next_refresh()
    {
        var router = CreateRouter();
        var log = new CallLog();
        var fail = false;
        var shown = new List<int>();
        using var binding = new RegionItemsBinding(router, views =>
        {
            if (fail) throw new InvalidOperationException("fails");
            shown.Add(views.Count);
        });

        fail = true;
        await GoToAsync(router, "A", log);                           // the show fails (reported, the route change succeeds)
        fail = false;
        binding.Refresh();                                           // the same views: shown now

        Assert.Equal(1, shown[^1]);
    }

    [Fact]
    public void A_connection_whose_binding_fails_is_not_active_and_can_connect_again()
    {
        var connection = new RegionConnection();
        var provider = new Microsoft.Extensions.DependencyInjection.ServiceCollection()
            .AddSingleton<IRouter>(CreateRouter())
            .BuildServiceProvider();

        Assert.Throws<InvalidOperationException>(() => connection.Connect(provider, null, _ => throw new InvalidOperationException("bind fails")));
        Assert.False(connection.Active);
        Assert.False(connection.IsConnected);

        Assert.True(connection.Connect(provider, null, router => new RegionBinding(router, _ => { })));
        Assert.True(connection.Active);
        Assert.True(connection.IsConnected);
        connection.Disconnect();
    }
}

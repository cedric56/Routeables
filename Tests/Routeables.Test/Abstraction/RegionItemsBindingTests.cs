using Routeables.Abstraction;
using Routeables.Routing;
using Routeables.Regions;

namespace Routeables.Tests.Abstraction;

/// <summary>A host that shows every view of a region gets the list of the history, up to the current view.</summary>
public class RegionItemsBindingTests
{
    private static ServiceProvider Build() => RegionTestServices.Build(services => services.AddRegion("stack"));

    private static RouteableEntry<T> Entry<T>(string route) => new(route, null, route, route) { Region = "stack" };

    [Fact]
    public async Task The_list_grows_with_each_route_and_shrinks_with_back_and_pop()
    {
        using var provider = Build();
        var router = provider.GetRouter("stack");
        var lists = new List<IReadOnlyList<object>>();
        using var binding = new RegionItemsBinding(router, lists.Add);
        var ct = TestContext.Current.CancellationToken;

        Assert.Single(lists);
        Assert.Empty(lists[0]);

        await provider.GoToAsync(Entry<RegionCatsViewModel>("/cats"), ct);
        await provider.GoToAsync(Entry<RegionDogsViewModel>("/dogs"), ct);
        var last = lists[^1];
        Assert.Equal(2, last.Count);
        Assert.IsType<RegionView<RegionCatsViewModel>>(last[0]);
        Assert.IsType<RegionView<RegionDogsViewModel>>(last[1]);
        Assert.Same(router.CurrentView, last[^1]);

        // Back keeps the entry for Forward, but it is not on the list: the last view is always the current one.
        await router.GoBackAsync(ct);
        Assert.Single(lists[^1]);
        Assert.Same(router.CurrentView, lists[^1][^1]);

        await router.ForwardAsync(ct);
        Assert.Equal(2, lists[^1].Count);

        await router.PopAsync(ct);
        Assert.Single(lists[^1]);
    }

    [Fact]
    public async Task The_list_is_given_again_only_when_it_changed_and_never_after_dispose()
    {
        using var provider = Build();
        var router = provider.GetRouter("stack");
        var lists = new List<IReadOnlyList<object>>();
        var binding = new RegionItemsBinding(router, lists.Add);
        var ct = TestContext.Current.CancellationToken;

        await provider.GoToAsync(Entry<RegionCatsViewModel>("/cats"), ct);
        var count = lists.Count;
        binding.Refresh();
        Assert.Equal(count, lists.Count);

        binding.Dispose();
        await provider.GoToAsync(Entry<RegionDogsViewModel>("/dogs"), ct);
        Assert.Equal(count, lists.Count);
    }
}

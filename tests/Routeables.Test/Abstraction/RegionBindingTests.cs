using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Routing;
using Routeables.Regions;

namespace Routeables.Tests.Abstraction;

public sealed class RegionView<T> { }
public sealed class RegionCatsViewModel(IRouter router) { public IRouter Router { get; } = router; }
public sealed class RegionDogsViewModel { }

/// <summary>The services the region tests share: the regions they name, a router, and the /cats and /dogs pages.</summary>
internal static class RegionTestServices
{
    public static ServiceProvider Build(Action<IServiceCollection> addRegions)
    {
        var services = new ServiceCollection();
        addRegions(services);
        services.AddSingleton<IRouter>(sp => new Router(new RouterHistory(), new RouterGate(), new RouterLifecycle(sp), sp));
        services.RegisterRoute<RegionView<RegionCatsViewModel>, RegionCatsViewModel>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/cats");
        services.RegisterRoute<RegionView<RegionDogsViewModel>, RegionDogsViewModel>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/dogs");
        services.AddTransient<RegionCatsViewModel>(); services.AddTransient<RegionDogsViewModel>();
        return services.BuildServiceProvider();
    }
}

/// <summary>A region host shows the current view of a router, whoever routes it; an entry names its region.</summary>
public class RegionBindingTests
{
    private static ServiceProvider Build() => RegionTestServices.Build(services =>
    {
        services.AddRegion("left");
        services.AddRegion("right");
    });

    private static RouteableEntry<T> Entry<T>(string route, string? region) => new(route, null, route, route) { Region = region };

    [Fact]
    public async Task An_entry_goes_to_the_router_of_its_region_and_the_host_of_that_region_shows_it()
    {
        using var provider = Build();
        var shownLeft = new List<object?>(); var shownRight = new List<object?>(); var shownMain = new List<object?>();
        using var left = new RegionBinding(provider.GetRouter("left"), shownLeft.Add);
        using var right = new RegionBinding(provider.GetRouter("right"), shownRight.Add);
        using var main = new RegionBinding(provider.GetRouter(null), shownMain.Add);
        Assert.Single(shownLeft);
        Assert.Null(shownLeft[0]);

        var cats = Entry<RegionCatsViewModel>("/cats", "left");
        Assert.True((await provider.GoToAsync(cats, TestContext.Current.CancellationToken)).Success);
        Assert.Equal(2, shownLeft.Count);
        Assert.IsType<RegionView<RegionCatsViewModel>>(shownLeft[1]);
        Assert.Single(shownRight);
        Assert.Single(shownMain);

        var dogs = Entry<RegionDogsViewModel>("/dogs", "right");
        await provider.GoToAsync(dogs, TestContext.Current.CancellationToken);
        Assert.IsType<RegionView<RegionDogsViewModel>>(shownRight[1]);
        Assert.Equal(2, shownLeft.Count);

        var main2 = Entry<RegionDogsViewModel>("/dogs", null);
        await provider.GoToAsync(main2, TestContext.Current.CancellationToken);
        Assert.IsType<RegionView<RegionDogsViewModel>>(shownMain[1]);
        Assert.Equal(2, shownLeft.Count);
        Assert.Equal(2, shownRight.Count);
    }

    [Fact]
    public async Task The_host_follows_back_forward_erase_and_stops_on_dispose()
    {
        using var provider = Build();
        var router = provider.GetRouter("left");
        var shown = new List<object?>();
        var binding = new RegionBinding(router, shown.Add);

        await provider.GoToAsync(Entry<RegionCatsViewModel>("/cats", "left"), TestContext.Current.CancellationToken);
        var cats = shown[^1];
        await provider.GoToAsync(Entry<RegionDogsViewModel>("/dogs", "left"), TestContext.Current.CancellationToken);
        Assert.NotSame(cats, shown[^1]);
        await router.GoBackAsync(TestContext.Current.CancellationToken);
        Assert.Same(cats, shown[^1]);
        await router.ForwardAsync(TestContext.Current.CancellationToken);
        Assert.IsType<RegionView<RegionDogsViewModel>>(shown[^1]);

        var before = shown.Count;
        binding.Refresh();
        Assert.Equal(before, shown.Count);               // nothing changed: nothing shown

        await router.EraseHistoryAsync(TestContext.Current.CancellationToken);
        Assert.Null(shown[^1]);

        binding.Dispose();
        var after = shown.Count;
        await provider.GoToAsync(Entry<RegionCatsViewModel>("/cats", "left"), TestContext.Current.CancellationToken);
        Assert.Equal(after, shown.Count);
    }

    [Fact]
    public void An_unknown_region_says_so()
    {
        using var provider = Build();
        Assert.Throws<InvalidOperationException>(() => provider.GetRouter("nowhere"));
    }

    [Fact]
    public async Task A_view_model_receives_the_router_that_builds_it()
    {
        using var provider = Build();
        var main = provider.GetRouter(null);
        var left = provider.GetRouter("left");
        var cts = TestContext.Current.CancellationToken;

        await provider.GoToAsync(Entry<RegionCatsViewModel>("/cats", "left"), cts);
        Assert.Same(left, ((RegionCatsViewModel)left.CurrentViewModel!).Router);

        await provider.GoToAsync(Entry<RegionCatsViewModel>("/cats", null), cts);
        Assert.Same(main, ((RegionCatsViewModel)main.CurrentViewModel!).Router);

        // A view model that takes no router is built as before.
        await provider.GoToAsync(Entry<RegionDogsViewModel>("/dogs", "left"), cts);
        Assert.IsType<RegionDogsViewModel>(left.CurrentViewModel);
    }

    [Fact]
    public async Task A_router_given_to_the_view_model_is_kept()
    {
        using var provider = Build();
        var right = provider.GetRouter("right");
        var entry = Entry<RegionCatsViewModel>("/cats", "left");
        entry.Arguments = RouteableArgs.Create().ForViewModel(right);

        await provider.GoToAsync(entry, TestContext.Current.CancellationToken);
        Assert.Same(right, ((RegionCatsViewModel)provider.GetRouter("left").CurrentViewModel!).Router);
    }
}

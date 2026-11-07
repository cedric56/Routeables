using Routeables.Abstraction;
using Routeables.Core;
using Routeables.Regions;
using Routeables.Routing;

namespace Routeables.Tests.Core;

/// <summary>An application without a menu: the main view model the hosts register for <c>UseRouteables()</c>, and the region that starts the routing.</summary>
public class DefaultMainViewModelTests
{
    public sealed class HomeView : IRouteableFor { public object? Context { get; set; } }
    public sealed class HomeVm;
    public sealed class DetailView : IRouteableFor { public object? Context { get; set; } }
    public sealed class DetailVm;

    private static ServiceProvider Build(string homeRoute = "/")
    {
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.AddRegion("detail");
        services.RegisterRoute<HomeView, HomeVm>(route: homeRoute);
        services.RegisterRoute<DetailView, DetailVm>(route: "/empty");
        services.RegisterMainViewModel<DefaultMainViewModel>(ServiceLifetime.Singleton);
        return services.BuildServiceProvider();
    }

    private static async Task UntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("The condition was not met in 5 seconds.");
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public void The_default_main_view_model_has_no_entry()
    {
        using var provider = Build();

        Assert.Empty(provider.GetRequiredService<IMainViewModel>().Entries);
    }

    [Fact]
    public async Task The_root_is_opened_when_the_default_region_starts_the_routing()
    {
        using var provider = Build();
        var main = provider.GetRequiredService<IMainViewModel>();

        Assert.True(main.Start());

        await UntilAsync(() => provider.GetRequiredService<IRouter>().CurrentViewModel is HomeVm);
        await UntilAsync(() => provider.GetRequiredService<IRouter>().CurrentView is HomeView);
    }

    [Fact]
    public void Selecting_the_first_entry_does_not_start_an_application_without_a_menu()
    {
        using var provider = Build();
        var main = provider.GetRequiredService<IMainViewModel>();

        Assert.False(main.SelectFirst());
        Assert.Null(provider.GetRequiredService<IRouter>().CurrentView);
    }

    [Fact]
    public async Task The_same_main_view_model_starts_only_once()
    {
        using var provider = Build();
        var main = provider.GetRequiredService<IMainViewModel>();

        Assert.True(main.Start());
        Assert.False(main.Start());     // two regions, or the region attached again, before the first page is there

        await UntilAsync(() => provider.GetRequiredService<IRouter>().CurrentViewModel is HomeVm);
        Assert.False(main.Start());
        Assert.Single(provider.GetRequiredService<IRouter>().History.Entries);
    }

    [Fact]
    public async Task A_region_with_a_Route_opens_that_page_and_the_main_view_model_follows()
    {
        using var provider = Build("/home");
        var router = provider.GetRequiredService<IRouter>();

        RegionStart.Begin(provider, null, "/home");

        await UntilAsync(() => router.CurrentViewModel is HomeVm);
        await UntilAsync(() => router.CurrentView is HomeView);
    }

    [Fact]
    public async Task A_region_starts_once_for_its_router_whatever_the_number_of_regions_that_show_it()
    {
        using var provider = Build("/home");
        var router = provider.GetRequiredService<IRouter>();

        RegionStart.Begin(provider, null, "/home");
        RegionStart.Begin(provider, null, "/home");
        await UntilAsync(() => router.CurrentViewModel is HomeVm);
        RegionStart.Begin(provider, null, "/home");
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Single(router.History.Entries);
    }

    [Fact]
    public async Task A_named_region_with_a_Route_opens_it_in_its_own_router_and_one_without_starts_nothing()
    {
        using var provider = Build("/home");
        var detail = provider.GetRouter("detail");

        RegionStart.Begin(provider, "detail");
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.Null(detail.CurrentView);

        RegionStart.Begin(provider, "detail", "/empty");
        await UntilAsync(() => detail.CurrentViewModel is DetailVm);
        Assert.Null(provider.GetRequiredService<IRouter>().CurrentView);
    }

    [Fact]
    public async Task A_host_that_waits_keeps_the_Route_until_it_starts_the_default_region()
    {
        using var provider = Build("/home");
        var router = provider.GetRequiredService<IRouter>();

        RegionStart.Begin(provider, null, "/home", wait: true);
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.Null(router.CurrentView);        // an address may choose the first page

        RegionStart.StartDefault(provider);
        await UntilAsync(() => router.CurrentViewModel is HomeVm);
    }

    [Fact]
    public async Task A_region_does_not_open_its_Route_over_a_page_that_is_already_shown()
    {
        using var provider = Build("/home");
        var router = provider.GetRequiredService<IRouter>();
        Assert.True((await router.GoToAsync("/home", cancellationToken: TestContext.Current.CancellationToken)).Success);

        RegionStart.Begin(provider, null, "/home");
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Single(router.History.Entries);
    }

    [Fact]
    public void A_region_does_nothing_when_the_application_has_no_main_view_model()
    {
        var services = new ServiceCollection();
        services.RegisterRouting();
        using var provider = services.BuildServiceProvider();

        RegionStart.Begin(provider, null);
    }
}

using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Routing;
using Routeables.Wpf;
using Application = System.Windows.Application;
using Window = System.Windows.Window;

namespace Routeables.Tests.Wpf;

/// <summary>
/// <see cref="RouteableRegion"/> and <see cref="RouteableRegionItems"/> show what the router of their region displays, follow it,
/// and let go of the views when they are unloaded (a view has one parent).
/// </summary>
[Collection(WpfCollection.Name)]
public class WpfRegionTests
{
    private sealed class CatsView : RouteableFor;
    private sealed class DogsView : RouteableFor;
    private sealed class CatsViewModel;
    private sealed class DogsViewModel;

    private static ServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.AddRegion("detail");
        services.AddSingleton<IRouter>(sp => new Router(new RouterHistory(), new RouterGate(), new RouterLifecycle(sp), sp));
        services.RegisterRoute<CatsView, CatsViewModel>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/cats");
        services.RegisterRoute<DogsView, DogsViewModel>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/dogs");
        services.AddTransient<CatsViewModel>();
        services.AddTransient<DogsViewModel>();
        return services.BuildServiceProvider();
    }

    private static RouteableEntry<CatsViewModel> Cats(string? region) => new("Cats", null, "/cats", "/cats") { Region = region };

    private static RouteableEntry<DogsViewModel> Dogs(string? region) => new("Dogs", null, "/dogs", "/dogs") { Region = region };

    // The views are created, and the controls loaded, on the thread of the application.
    private static async Task GoToAsync<T>(IServiceProvider provider, RouteableEntry<T> entry)
    {
        var result = await WpfHarness.Run(() => provider.GoToAsync(entry, TestContext.Current.CancellationToken));
        Assert.True(result.Success);
    }

    private static Window Show(UIElement content)
    {
        var window = new Window { Content = content, Width = 300, Height = 300, ShowActivated = false, ShowInTaskbar = false };
        window.Show();
        return window;
    }

    private static async Task CloseAsync(params Window[] windows)
    {
        await WpfHarness.Run(() =>
        {
            foreach (var window in windows)
                window.Close();
        });
        await WpfHarness.Idle();
    }

    [Fact]
    public async Task A_region_shows_the_view_of_its_router_and_follows_it()
    {
        using var provider = Build();
        var region = await WpfHarness.Run(() => new RouteableRegion { Region = "detail", Services = provider });
        var window = await WpfHarness.Run(() => Show(region));
        try
        {
            await WpfHarness.Idle();
            Assert.Null(await WpfHarness.Run(() => region.Content));

            await GoToAsync(provider, Cats("detail"));
            var first = await WpfHarness.Run(() => region.Content);
            Assert.IsType<CatsView>(first);
            Assert.IsType<CatsViewModel>(await WpfHarness.Run(() => ((CatsView)first!).DataContext));

            await GoToAsync(provider, Dogs("detail"));
            Assert.IsType<DogsView>(await WpfHarness.Run(() => region.Content));
        }
        finally
        {
            await CloseAsync(window);
        }
    }

    [Fact]
    public async Task A_region_ignores_what_the_other_routers_display()
    {
        using var provider = Build();
        var region = await WpfHarness.Run(() => new RouteableRegion { Region = "detail", Services = provider });
        var window = await WpfHarness.Run(() => Show(region));
        try
        {
            await WpfHarness.Idle();

            await GoToAsync(provider, Cats(null));

            Assert.Null(await WpfHarness.Run(() => region.Content));
        }
        finally
        {
            await CloseAsync(window);
        }
    }

    [Fact]
    public async Task Changing_the_region_shows_the_view_of_the_other_router()
    {
        using var provider = Build();
        await GoToAsync(provider, Cats("detail"));
        await GoToAsync(provider, Dogs(null));
        var region = await WpfHarness.Run(() => new RouteableRegion { Region = "detail", Services = provider });
        var window = await WpfHarness.Run(() => Show(region));
        try
        {
            await WpfHarness.Idle();
            // A router that already displays a view gives it as soon as the region is loaded.
            Assert.IsType<CatsView>(await WpfHarness.Run(() => region.Content));

            await WpfHarness.Run(() => region.Region = null);

            Assert.IsType<DogsView>(await WpfHarness.Run(() => region.Content));
        }
        finally
        {
            await CloseAsync(window);
        }
    }

    [Fact]
    public async Task An_unloaded_region_lets_go_of_the_view_and_does_not_show_a_later_one()
    {
        using var provider = Build();
        var region = await WpfHarness.Run(() => new RouteableRegion { Region = "detail", Services = provider });
        var window = await WpfHarness.Run(() => Show(region));
        await WpfHarness.Idle();
        await GoToAsync(provider, Cats("detail"));
        var view = (CatsView)(await WpfHarness.Run(() => region.Content))!;

        await CloseAsync(window);

        Assert.Null(await WpfHarness.Run(() => region.Content));
        Assert.Null(await WpfHarness.Run(() => LogicalTreeHelper.GetParent(view)));

        // The router goes on; the region that is gone shows nothing.
        await GoToAsync(provider, Dogs("detail"));
        await WpfHarness.Idle();
        Assert.Null(await WpfHarness.Run(() => region.Content));
    }

    [Fact]
    public async Task The_view_of_an_unloaded_region_can_be_shown_by_another_one()
    {
        using var provider = Build();
        var first = await WpfHarness.Run(() => new RouteableRegion { Region = "detail", Services = provider });
        var firstWindow = await WpfHarness.Run(() => Show(first));
        await WpfHarness.Idle();
        await GoToAsync(provider, Cats("detail"));
        var view = await WpfHarness.Run(() => first.Content);
        await CloseAsync(firstWindow);

        var second = await WpfHarness.Run(() => new RouteableRegion { Region = "detail", Services = provider });
        var secondWindow = await WpfHarness.Run(() => Show(second));
        try
        {
            await WpfHarness.Idle();

            Assert.Same(view, await WpfHarness.Run(() => second.Content));
        }
        finally
        {
            await CloseAsync(secondWindow);
        }
    }

    [Fact]
    public async Task A_region_that_wants_a_view_held_by_another_one_says_who_holds_it()
    {
        using var provider = Build();
        var holder = await WpfHarness.Run(() => new RouteableRegion { Region = "detail", Services = provider });
        var holderWindow = await WpfHarness.Run(() => Show(holder));
        await WpfHarness.Idle();
        await GoToAsync(provider, Cats("detail"));

        // The second region is connected to the default router first, then to the region whose view the first one holds.
        var other = await WpfHarness.Run(() => new RouteableRegion { Services = provider });
        var otherWindow = await WpfHarness.Run(() => Show(other));
        try
        {
            await WpfHarness.Idle();

            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => WpfHarness.Run(() => { other.Region = "detail"; }));

            Assert.Contains("CatsView", error.Message);
            Assert.Contains("RouteableRegion", error.Message);
            Assert.Contains("detail", error.Message);
        }
        finally
        {
            await CloseAsync(otherWindow, holderWindow);
        }
    }

    [Fact]
    public async Task Without_a_container_a_region_stays_empty()
    {
        ServiceLocator.Services = null;
        var region = await WpfHarness.Run(() => new RouteableRegion { Region = "detail" });
        var window = await WpfHarness.Run(() => Show(region));
        try
        {
            await WpfHarness.Idle();

            Assert.Null(await WpfHarness.Run(() => region.Content));
        }
        finally
        {
            await CloseAsync(window);
        }
    }

    [Fact]
    public async Task A_region_of_views_shows_the_stack_of_its_router_and_follows_pop()
    {
        using var provider = Build();
        var region = await WpfHarness.Run(() => new RouteableRegionItems { Region = "detail", Services = provider });
        var window = await WpfHarness.Run(() => Show(region));
        try
        {
            await WpfHarness.Idle();
            Assert.Equal(0, await WpfHarness.Run(() => region.Items.Count));

            await GoToAsync(provider, Cats("detail"));
            await GoToAsync(provider, Dogs("detail"));
            Assert.Equal(2, await WpfHarness.Run(() => region.Items.Count));
            Assert.IsType<CatsView>(await WpfHarness.Run(() => region.Items[0]));
            Assert.IsType<DogsView>(await WpfHarness.Run(() => region.Items[1]));

            await WpfHarness.Run(() => provider.GetRouter("detail").PopAsync(TestContext.Current.CancellationToken));
            await WpfHarness.WaitUntil(() => region.Items.Count == 1, "the popped view leaves the region", TestContext.Current.CancellationToken);
            Assert.IsType<CatsView>(await WpfHarness.Run(() => region.Items[0]));
        }
        finally
        {
            await CloseAsync(window);
        }
    }

    [Fact]
    public async Task A_region_of_views_lays_them_out_in_a_wrap_panel_by_default()
    {
        var panel = await WpfHarness.Run(() => new RouteableRegionItems().ItemsPanel.LoadContent());

        Assert.IsType<WrapPanel>(panel);
    }

    [Fact]
    public async Task An_unloaded_region_of_views_lets_go_of_them()
    {
        using var provider = Build();
        var region = await WpfHarness.Run(() => new RouteableRegionItems { Region = "detail", Services = provider });
        var window = await WpfHarness.Run(() => Show(region));
        await WpfHarness.Idle();
        await GoToAsync(provider, Cats("detail"));
        await GoToAsync(provider, Dogs("detail"));
        var views = await WpfHarness.Run(() => region.Items.Cast<DependencyObject>().ToArray());
        Assert.Equal(2, views.Length);

        await CloseAsync(window);

        Assert.Equal(0, await WpfHarness.Run(() => region.Items.Count));
        var parents = await WpfHarness.Run(() => views.Select(view => LogicalTreeHelper.GetParent(view)).ToArray());
        Assert.All(parents, parent => Assert.Null(parent));
    }

    [Fact]
    public async Task A_region_of_views_that_wants_a_view_held_by_another_control_says_who_holds_it()
    {
        using var provider = Build();
        var holder = await WpfHarness.Run(() => new RouteableRegion { Region = "detail", Services = provider });
        var holderWindow = await WpfHarness.Run(() => Show(holder));
        await WpfHarness.Idle();
        await GoToAsync(provider, Cats("detail"));

        var items = await WpfHarness.Run(() => new RouteableRegionItems { Services = provider });
        var itemsWindow = await WpfHarness.Run(() => Show(items));
        try
        {
            await WpfHarness.Idle();

            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => WpfHarness.Run(() => { items.Region = "detail"; }));

            Assert.Contains("CatsView", error.Message);
            Assert.Contains("RouteableRegion", error.Message);
        }
        finally
        {
            await CloseAsync(itemsWindow, holderWindow);
        }
    }
}

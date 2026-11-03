using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Routing;
using Routeables.Wpf;
using Window = System.Windows.Window;

namespace Routeables.Tests.Wpf;

/// <summary><see cref="RouteableTabs.AttachRouteables"/>: the tabs of a <see cref="TabControl"/> are routes, and follow the router.</summary>
[Collection(WpfCollection.Name)]
public class WpfTabsTests
{
    private sealed class CatsView : RouteableFor;
    private sealed class DogsView : RouteableFor;
    private sealed class CatsViewModel;
    private sealed class DogsViewModel;

    private static ServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRouter>(sp => new Router(new RouterHistory(), new RouterGate(), new RouterLifecycle(sp), sp));
        services.RegisterRoute<CatsView, CatsViewModel>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/cats");
        services.RegisterRoute<DogsView, DogsViewModel>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/dogs");
        services.AddTransient<CatsViewModel>();
        services.AddTransient<DogsViewModel>();
        return services.BuildServiceProvider();
    }

    private static TabItem Tab(string header, string route)
    {
        var tab = new TabItem { Header = header };
        RouteableItem.SetRoute(tab, route);
        return tab;
    }

    private static TabControl Tabs() => new() { Items = { Tab("Cats", "cats"), Tab("Dogs", "dogs") } };

    [Fact]
    public async Task Choosing_a_tab_shows_the_page_of_its_route_in_that_tab()
    {
        using var provider = Build();
        var tabs = await WpfHarness.Run(Tabs);
        var window = await WpfHarness.Run(() => { var w = new Window { Content = tabs, ShowActivated = false, ShowInTaskbar = false }; w.Show(); return w; });
        IDisposable? attached = null;
        try
        {
            attached = await WpfHarness.Run(() => tabs.AttachRouteables(provider));
            await WpfHarness.Idle();
            Assert.IsType<CatsView>(await WpfHarness.Run(() => ((RouteableRegion)((TabItem)tabs.Items[0]!).Content).Content));

            await WpfHarness.Run(() => tabs.SelectedIndex = 1);
            await WpfHarness.Idle();
            await WpfHarness.Idle();

            var second = (TabItem)await WpfHarness.Run(() => tabs.Items[1]!);
            Assert.IsType<DogsView>(await WpfHarness.Run(() => ((RouteableRegion)second.Content).Content));
            Assert.Null(await WpfHarness.Run(() => ((TabItem)tabs.Items[0]!).Content));
        }
        finally
        {
            // The tabs belong to the thread of the application: so does the end of the subscription.
            await WpfHarness.Run(() => { attached?.Dispose(); window.Close(); });
        }
    }

    [Fact]
    public async Task A_route_reached_by_the_router_selects_its_tab()
    {
        using var provider = Build();
        var tabs = await WpfHarness.Run(Tabs);
        var window = await WpfHarness.Run(() => { var w = new Window { Content = tabs, ShowActivated = false, ShowInTaskbar = false }; w.Show(); return w; });
        IDisposable? attached = null;
        try
        {
            attached = await WpfHarness.Run(() => tabs.AttachRouteables(provider));
            await WpfHarness.Idle();

            var result = await WpfHarness.Run(() => provider.GetRequiredService<IRouter>().GoToAsync("/dogs", cancellationToken: TestContext.Current.CancellationToken));
            Assert.True(result.Success);
            await WpfHarness.Idle();
            await WpfHarness.Idle();

            Assert.Equal(1, await WpfHarness.Run(() => tabs.SelectedIndex));
        }
        finally
        {
            // The tabs belong to the thread of the application: so does the end of the subscription.
            await WpfHarness.Run(() => { attached?.Dispose(); window.Close(); });
        }
    }
}

using Avalonia.Controls;
using Avalonia.Threading;
using Routeables.Abstraction.Routing;
using Routeables.Avalonia;
using Xunit;

namespace Routeables.Test.Avalonia;

/// <summary>A <see cref="TabControl"/> written in XAML whose tabs are routes: choosing a tab routes, and a route that something else reached selects its tab.</summary>
[Collection("Avalonia headless")]
public sealed class HeadlessTabsTests(HeadlessApp app)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private Task RunAsync(Func<IServiceProvider, IRouter, TabControl, Task> test) => app.RunAsync(async () =>
    {
        var provider = app.Provider;
        var router = provider.GetRouter(HeadlessApp.Side);
        await router.EraseHistoryAsync();

        var first = new TabItem { Header = "A" };
        var second = new TabItem { Header = "B" };
        RouteableItem.SetRoute(first, "/a");
        RouteableItem.SetRoute(second, "/b");
        var tabs = new TabControl();
        tabs.Items.Add(first);
        tabs.Items.Add(second);
        var window = new Window { Width = 400, Height = 300, Content = tabs };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            await test(provider, router, tabs);
        }
        finally
        {
            window.Close();
            await router.EraseHistoryAsync();
        }
    });

    private static async Task UntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.Add(Timeout);
        while (true)
        {
            Dispatcher.UIThread.RunJobs();
            if (condition()) return;
            if (DateTime.UtcNow > deadline) throw new TimeoutException("The condition was not met in 5 seconds.");
            await Task.Delay(10);
        }
    }

    [Fact]
    public Task Choosing_a_tab_routes_and_a_route_selects_its_tab() => RunAsync(async (provider, router, tabs) =>
    {
        tabs.SelectedIndex = 0;
        using var attached = tabs.AttachRouteables(provider, HeadlessApp.Side);
        await UntilAsync(() => router.CurrentViewModel is PageAViewModel);

        tabs.SelectedIndex = 1;
        await UntilAsync(() => router.CurrentViewModel is PageBViewModel);

        await provider.GoToAsync("/a", HeadlessApp.Side);
        await UntilAsync(() => tabs.SelectedIndex == 0);
    });

    [Fact]
    public Task The_selected_tab_holds_the_region_that_shows_the_page() => RunAsync(async (provider, router, tabs) =>
    {
        tabs.SelectedIndex = 0;
        using var attached = tabs.AttachRouteables(provider, HeadlessApp.Side);
        await UntilAsync(() => router.CurrentViewModel is PageAViewModel);

        var region = Assert.IsType<RouteableRegion>(((TabItem)tabs.SelectedItem!).Content);
        Assert.Equal(HeadlessApp.Side, region.Region);

        tabs.SelectedIndex = 1;
        await UntilAsync(() => ((TabItem)tabs.SelectedItem!).Content is RouteableRegion);
    });

    [Fact]
    public Task A_disposed_attachment_stops_following_the_tabs() => RunAsync(async (provider, router, tabs) =>
    {
        tabs.SelectedIndex = 0;
        var attached = tabs.AttachRouteables(provider, HeadlessApp.Side);
        await UntilAsync(() => router.CurrentViewModel is PageAViewModel);

        attached.Dispose();
        tabs.SelectedIndex = 1;
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(50);
        Dispatcher.UIThread.RunJobs();

        Assert.IsType<PageAViewModel>(router.CurrentViewModel);
    });

    [Fact]
    public Task The_control_and_the_container_are_required() => RunAsync((provider, router, tabs) =>
    {
        Assert.Throws<ArgumentNullException>(() => ((TabControl)null!).AttachRouteables(provider));
        Assert.Throws<ArgumentNullException>(() => tabs.AttachRouteables(null!));
        return Task.CompletedTask;
    });
}

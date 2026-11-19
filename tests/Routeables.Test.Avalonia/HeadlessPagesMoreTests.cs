using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Threading;
using Routeables.Abstraction;
using Routeables.Abstraction.Routing;
using Routeables.Avalonia;
using Xunit;

namespace Routeables.Test.Avalonia;

/// <summary>
/// What the first tests of the page controls and of the regions leave out: the header of a page, a drawer that stays open, what disposing gives
/// back, and what a screen reader reads of the controls (the automation peers of the region and of the views).
/// </summary>
[Collection("Avalonia headless")]
public sealed class HeadlessPagesMoreTests(HeadlessApp app)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private Task RunAsync(Func<IServiceProvider, IRouter, Task> test) => app.RunAsync(async () =>
    {
        var provider = app.Provider;
        var router = provider.GetRouter(HeadlessApp.Side);
        await router.EraseHistoryAsync();
        await test(provider, router);
        await router.EraseHistoryAsync();
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

    private static Window Show(Control content)
    {
        var window = new Window { Width = 400, Height = 300, Content = content };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    [Fact]
    public Task The_header_of_a_page_is_the_name_of_its_view_model_in_words_unless_the_caller_gives_one() => RunAsync(async (provider, router) =>
    {
        var navigation = new NavigationPage();
        var window = Show(navigation);
        using var attached = navigation.AttachRouteables(provider, HeadlessApp.Side);
        try
        {
            await provider.GoToAsync("/a", HeadlessApp.Side);
            await UntilAsync(() => navigation.StackDepth == 1 && !navigation.IsNavigating);
            Assert.Equal("Page A", ((ContentPage)navigation.NavigationStack[0]).Header);
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task A_header_given_by_the_caller_replaces_the_default_one() => RunAsync(async (provider, router) =>
    {
        var navigation = new NavigationPage();
        var window = Show(navigation);
        using var attached = navigation.AttachRouteables(provider, HeadlessApp.Side, header: entry => "Title of " + entry.ViewModel.GetType().Name);
        try
        {
            await provider.GoToAsync("/b", HeadlessApp.Side);
            await UntilAsync(() => navigation.StackDepth == 1 && !navigation.IsNavigating);
            Assert.Equal("Title of PageBViewModel", ((ContentPage)navigation.NavigationStack[0]).Header);
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task A_drawer_that_does_not_close_on_a_route_stays_open() => RunAsync(async (provider, router) =>
    {
        var drawer = new DrawerPage { Drawer = new TextBlock { Text = "menu" }, IsOpen = true };
        var window = Show(drawer);
        using var attached = drawer.AttachRouteables(provider, HeadlessApp.Side, closeOnRoute: false);
        try
        {
            var navigation = Assert.IsType<NavigationPage>(drawer.Content);

            await provider.GoToAsync("/a", HeadlessApp.Side);
            await UntilAsync(() => navigation.StackDepth == 1 && !navigation.IsNavigating);
            await Task.Delay(100);
            Dispatcher.UIThread.RunJobs();

            Assert.True(drawer.IsOpen);
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task Disposing_a_drawer_takes_away_the_page_it_made() => RunAsync((provider, _) =>
    {
        var drawer = new DrawerPage { Drawer = new TextBlock { Text = "menu" } };
        var window = Show(drawer);
        try
        {
            var attached = drawer.AttachRouteables(provider, HeadlessApp.Side);
            Assert.IsType<NavigationPage>(drawer.Content);

            attached.Dispose();

            Assert.Null(drawer.Content);
        }
        finally { window.Close(); }

        return Task.CompletedTask;
    });

    [Fact]
    public Task A_navigation_page_that_the_caller_made_is_kept_when_the_drawer_is_disposed() => RunAsync((provider, _) =>
    {
        var navigation = new NavigationPage();
        var drawer = new DrawerPage { Drawer = new TextBlock { Text = "menu" }, Content = navigation };
        var window = Show(drawer);
        try
        {
            drawer.AttachRouteables(provider, HeadlessApp.Side).Dispose();

            Assert.Same(navigation, drawer.Content);
        }
        finally { window.Close(); }

        return Task.CompletedTask;
    });

    [Fact]
    public Task The_attach_methods_refuse_null() => RunAsync((provider, _) =>
    {
        Assert.Throws<ArgumentNullException>(() => ((NavigationPage)null!).AttachRouteables(provider));
        Assert.Throws<ArgumentNullException>(() => new NavigationPage().AttachRouteables(null!));
        Assert.Throws<ArgumentNullException>(() => ((DrawerPage)null!).AttachRouteables(provider));
        Assert.Throws<ArgumentNullException>(() => new DrawerPage().AttachRouteables(null!));
        Assert.Throws<ArgumentNullException>(() => ((TabbedPage)null!).AttachRouteables(provider));
        Assert.Throws<ArgumentNullException>(() => new TabbedPage().AttachRouteables(null!));
        return Task.CompletedTask;
    });

    [Fact]
    public Task A_region_is_a_group_named_after_its_region_for_a_screen_reader() => app.RunAsync(() =>
    {
        var side = ControlAutomationPeer.CreatePeerForElement(new RouteableRegion { Region = HeadlessApp.Side });
        Assert.Equal(AutomationControlType.Group, side.GetAutomationControlType());
        Assert.Equal(HeadlessApp.Side, side.GetName());
        Assert.Equal("Region." + HeadlessApp.Side, side.GetAutomationId());
        Assert.Equal("RouteableRegion", side.GetClassName());

        var main = ControlAutomationPeer.CreatePeerForElement(new RouteableRegion());
        Assert.Equal("main", main.GetName());
        Assert.Equal("Region.main", main.GetAutomationId());
        return Task.CompletedTask;
    });

    [Fact]
    public Task The_name_and_the_id_the_author_gave_win_over_the_ones_of_the_peer() => app.RunAsync(() =>
    {
        var region = new RouteableRegion { Region = HeadlessApp.Side };
        AutomationProperties.SetName(region, "Content");
        AutomationProperties.SetAutomationId(region, "content-region");

        var peer = ControlAutomationPeer.CreatePeerForElement(region);

        Assert.Equal("Content", peer.GetName());
        Assert.Equal("content-region", peer.GetAutomationId());
        return Task.CompletedTask;
    });

    [Fact]
    public Task A_view_is_a_group_named_after_its_view_model_without_the_suffix() => app.RunAsync(() =>
    {
        var peer = ControlAutomationPeer.CreatePeerForElement(new PageAView());

        Assert.Equal(AutomationControlType.Group, peer.GetAutomationControlType());
        Assert.Equal("PageA", peer.GetName());
        Assert.Equal("View.PageA", peer.GetAutomationId());
        Assert.Equal("RouteableFor", peer.GetClassName());
        return Task.CompletedTask;
    });
}

using Avalonia.Controls;
using Avalonia.Threading;
using Routeables.Abstraction;
using Routeables.Abstraction.Routing;
using Routeables.Avalonia;
using Xunit;

namespace Routeables.Test.Avalonia;

/// <summary>
/// The page controls of Avalonia 12 in a real (headless) window, driven through the side router: the pile of a <see cref="NavigationPage"/> follows the
/// history, the back button goes through the router (which can refuse), a <see cref="DrawerPage"/> closes after a route change and a
/// <see cref="TabbedPage"/> selects the tab of a route.
/// </summary>
[Collection("Avalonia headless")]
public sealed class HeadlessPagesTests(HeadlessApp app)
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

    // The test pages are RouteableFor<...> views whose content is a TextBlock "A" or "B".
    private static string? Label(Page page)
        => ((((page as ContentPage)?.Content as ContentControl)?.Content) as TextBlock)?.Text;

    [Fact]
    public Task A_NavigationPage_pushes_a_page_for_each_route_and_pops_it_on_Back() => RunAsync(async (provider, router) =>
    {
        var navigation = new NavigationPage();
        var window = Show(navigation);
        using var attached = navigation.AttachRouteables(provider, HeadlessApp.Side);
        try
        {
            await provider.GoToAsync("/a", HeadlessApp.Side);
            await provider.GoToAsync("/b", HeadlessApp.Side);
            await UntilAsync(() => navigation.StackDepth == 2 && !navigation.IsNavigating);
            Assert.Equal(["A", "B"], navigation.NavigationStack.Select(Label));

            await router.GoBackAsync();
            await UntilAsync(() => navigation.StackDepth == 1 && !navigation.IsNavigating);
            Assert.Equal("A", Label(navigation.NavigationStack[0]));

            await router.ForwardAsync();
            await UntilAsync(() => navigation.StackDepth == 2 && !navigation.IsNavigating);
            Assert.Equal("B", Label(navigation.NavigationStack[1]));
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task The_back_button_of_a_NavigationPage_goes_through_the_router() => RunAsync(async (provider, router) =>
    {
        var navigation = new NavigationPage();
        var window = Show(navigation);
        using var attached = navigation.AttachRouteables(provider, HeadlessApp.Side);
        try
        {
            await provider.GoToAsync("/a", HeadlessApp.Side);
            await provider.GoToAsync("/b", HeadlessApp.Side);
            await UntilAsync(() => navigation.StackDepth == 2 && !navigation.IsNavigating);

            await navigation.PopAsync();      // what the back button does

            await UntilAsync(() => navigation.StackDepth == 1 && !navigation.IsNavigating);
            Assert.Equal(0, router.History.CurrentIndex);   // the router went back too
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task A_page_that_refuses_to_be_left_stays_when_the_user_goes_back() => RunAsync(async (provider, router) =>
    {
        var navigation = new NavigationPage();
        var window = Show(navigation);
        using var attached = navigation.AttachRouteables(provider, HeadlessApp.Side);
        try
        {
            await provider.GoToAsync("/b", HeadlessApp.Side);
            await provider.GoToAsync("/a", HeadlessApp.Side);
            await UntilAsync(() => navigation.StackDepth == 2 && !navigation.IsNavigating);
            ((PageAViewModel)router.CurrentViewModel!).CanLeave = false;

            await navigation.PopAsync();
            await Task.Delay(200);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(2, navigation.StackDepth);
            Assert.Equal("A", Label(navigation.NavigationStack[1]));
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task A_DrawerPage_gets_a_NavigationPage_and_closes_after_a_route_change() => RunAsync(async (provider, router) =>
    {
        var drawer = new DrawerPage { Drawer = new TextBlock { Text = "menu" }, IsOpen = true };
        var window = Show(drawer);
        using var attached = drawer.AttachRouteables(provider, HeadlessApp.Side);
        try
        {
            var navigation = Assert.IsType<NavigationPage>(drawer.Content);

            await provider.GoToAsync("/a", HeadlessApp.Side);

            await UntilAsync(() => navigation.StackDepth == 1 && !drawer.IsOpen);
            Assert.Equal("A", Label(navigation.NavigationStack[0]));
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task A_TabbedPage_routes_when_a_tab_is_chosen_and_selects_the_tab_of_a_route() => RunAsync(async (provider, router) =>
    {
        var first = new ContentPage { Header = "A" };
        var second = new ContentPage { Header = "B" };
        RouteableItem.SetRoute(first, "/a");
        RouteableItem.SetRoute(second, "/b");
        var tabs = new TabbedPage { Pages = new[] { first, second } };
        var window = Show(tabs);
        using var attached = tabs.AttachRouteables(provider, HeadlessApp.Side);
        try
        {
            await UntilAsync(() => router.CurrentViewModel is PageAViewModel);

            tabs.SelectedIndex = 1;
            await UntilAsync(() => router.CurrentViewModel is PageBViewModel);

            await provider.GoToAsync("/a", HeadlessApp.Side);
            await UntilAsync(() => tabs.SelectedIndex == 0);
        }
        finally { window.Close(); }
    });
}

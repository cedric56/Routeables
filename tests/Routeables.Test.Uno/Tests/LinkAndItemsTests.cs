using Microsoft.UI.Xaml.Automation.Peers;
using Routeables.Abstraction;
using Routeables.Abstraction.Routing;
using Routeables.Uno;
using Uno.UI.RuntimeTests;

namespace Routeables.Test.Uno.Tests;

/// <summary><see cref="RouteableLink"/> on a real button, and <see cref="RouteableRegionItems"/> in a real tree.</summary>
[TestClass]
[RunsOnUIThread]
public class LinkAndItemsTests
{
    /// <summary>Invokes the button through its automation peer (as a screen reader or keyboard would).</summary>
    private static void Click(Button button) => new ButtonAutomationPeer(button).Invoke();

    /// <summary>Resets the host, creates a button with the given route and region, loads it and returns it.</summary>
    private static async Task<Button> LoadedButtonAsync(string? route, string? region = null)
    {
        await TestHost.ResetAsync();
        var button = new Button { Content = "go" };
        RouteableLink.SetRoute(button, route);
        RouteableLink.SetRegion(button, region);
        return await TestHost.LoadAsync(button);
    }

    /// <summary>Clicking a link navigates to its route in the default region.</summary>
    [TestMethod]
    public async Task A_click_goes_to_the_route_in_the_default_region()
    {
        var button = await LoadedButtonAsync("/b");
        try
        {
            Click(button);

            await TestHost.UntilAsync(() => TestHost.Router().CurrentView is PageBView);
        }
        finally { await TestHost.UnloadAsync(button); }
    }

    /// <summary>Clicking a link navigates to its route in the region configured on the link.</summary>
    [TestMethod]
    public async Task A_click_goes_to_the_route_in_the_region_of_the_link()
    {
        var button = await LoadedButtonAsync("/b", App.Side);
        try
        {
            Click(button);

            await TestHost.UntilAsync(() => TestHost.Router(App.Side).CurrentView is PageBView);
            Assert.IsNull(TestHost.Router().CurrentView);
        }
        finally { await TestHost.UnloadAsync(button); }
    }

    /// <summary>A link without a route does nothing when clicked.</summary>
    [TestMethod]
    public async Task A_link_without_a_route_does_nothing()
    {
        var button = await LoadedButtonAsync("/b");
        try
        {
            RouteableLink.SetRoute(button, null);

            Click(button);

            await Task.Delay(200);
            Assert.IsNull(TestHost.Router().CurrentView);
        }
        finally { await TestHost.UnloadAsync(button); }
    }

    /// <summary>Changing the route several times is followed once per click; history grows by one entry.</summary>
    [TestMethod]
    public async Task A_route_changed_several_times_is_followed_once_per_click()
    {
        var button = await LoadedButtonAsync("/a");
        try
        {
            await TestHost.Services.GoToAsync("/a");
            RouteableLink.SetRoute(button, "/b");
            RouteableLink.SetRoute(button, "/b ");
            RouteableLink.SetRoute(button, "/b");

            Click(button);
            await TestHost.UntilAsync(() => TestHost.Router().CurrentView is PageBView);
            await TestHost.UntilAsync(() => TestHost.Router().History.Count == 2);

            // One entry was added: Back is the first page again.
            await TestHost.Router().GoBackAsync();
            Assert.IsInstanceOfType(TestHost.Router().CurrentView, typeof(PageAView));
        }
        finally { await TestHost.UnloadAsync(button); }
    }

    /// <summary>Navigating to a route nobody registered leaves the application on its current page.</summary>
    [TestMethod]
    public async Task A_route_nobody_registered_leaves_the_application_on_its_page()
    {
        var button = await LoadedButtonAsync("/nowhere");
        try
        {
            await TestHost.Services.GoToAsync("/a");

            Click(button);

            await Task.Delay(300);
            Assert.IsInstanceOfType(TestHost.Router().CurrentView, typeof(PageAView));
        }
        finally { await TestHost.UnloadAsync(button); }
    }

    /// <summary>The attached route and region properties can be read back from the element.</summary>
    [TestMethod]
    public void The_attached_properties_are_read_back()
    {
        var button = new Button();

        RouteableLink.SetRoute(button, "/a");
        RouteableLink.SetRegion(button, "side");

        Assert.AreEqual("/a", RouteableLink.GetRoute(button));
        Assert.AreEqual("side", RouteableLink.GetRegion(button));
    }

    // ------------------------------------------------------------------------------------------------ RouteableRegionItems

    /// <summary>Returns the views currently shown by the items control, oldest first.</summary>
    private static IReadOnlyList<object> Views(RouteableRegionItems items)
        => items.Content is ScrollViewer { Content: ItemsRepeater { ItemsSource: IEnumerable<object> source } } ? [.. source] : [];

    /// <summary>Resets the host, creates a <see cref="RouteableRegionItems"/> for the side region, optionally configures it, loads it and returns it.</summary>
    private static async Task<RouteableRegionItems> LoadedItemsAsync(Action<RouteableRegionItems>? configure = null)
    {
        await TestHost.ResetAsync();
        var items = new RouteableRegionItems { Region = App.Side };
        configure?.Invoke(items);
        return await TestHost.LoadAsync(items);
    }

    /// <summary>Every view of the history is shown, oldest first.</summary>
    [TestMethod]
    public async Task Every_view_of_the_history_is_shown_oldest_first()
    {
        var items = await LoadedItemsAsync();
        try
        {
            await TestHost.Services.GoToAsync("/a", App.Side);
            await TestHost.Services.GoToAsync("/b", App.Side);

            await TestHost.UntilAsync(() => Views(items).Count == 2);
            Assert.IsInstanceOfType(Views(items)[0], typeof(PageAView));
            Assert.IsInstanceOfType(Views(items)[1], typeof(PageBView));
        }
        finally { await TestHost.UnloadAsync(items); }
    }

    /// <summary>Going back removes the last view from the items control.</summary>
    [TestMethod]
    public async Task Going_back_removes_the_last_view()
    {
        var items = await LoadedItemsAsync();
        try
        {
            await TestHost.Services.GoToAsync("/a", App.Side);
            await TestHost.Services.GoToAsync("/b", App.Side);
            await TestHost.UntilAsync(() => Views(items).Count == 2);

            await TestHost.Router(App.Side).GoBackAsync();

            await TestHost.UntilAsync(() => Views(items).Count == 1);
            Assert.IsInstanceOfType(Views(items)[0], typeof(PageAView));
        }
        finally { await TestHost.UnloadAsync(items); }
    }

    /// <summary>Erasing the history leaves no view in the items control.</summary>
    [TestMethod]
    public async Task Erasing_the_history_leaves_no_view()
    {
        var items = await LoadedItemsAsync();
        try
        {
            await TestHost.Services.GoToAsync("/a", App.Side);
            await TestHost.UntilAsync(() => Views(items).Count == 1);

            await TestHost.Router(App.Side).EraseHistoryAsync();

            await TestHost.UntilAsync(() => Views(items).Count == 0);
        }
        finally { await TestHost.UnloadAsync(items); }
    }

    /// <summary>A control that leaves the visual tree releases its views.</summary>
    [TestMethod]
    public async Task A_control_that_leaves_the_tree_lets_go_of_its_views()
    {
        var items = await LoadedItemsAsync();
        await TestHost.Services.GoToAsync("/a", App.Side);
        await TestHost.UntilAsync(() => Views(items).Count == 1);

        await TestHost.UnloadAsync(items);

        Assert.IsNull(items.Content);
    }

    /// <summary>Spacing and minimum item width are applied to the underlying uniform grid layout.</summary>
    [TestMethod]
    public async Task The_width_and_the_spacing_are_given_to_the_layout()
    {
        var items = await LoadedItemsAsync(i => { i.Spacing = 20; i.MinItemWidth = 150; });
        try
        {
            await TestHost.Services.GoToAsync("/a", App.Side);
            await TestHost.UntilAsync(() => Views(items).Count == 1);

            var layout = (UniformGridLayout)((ItemsRepeater)((ScrollViewer)items.Content!).Content).Layout;
            Assert.AreEqual(150, layout.MinItemWidth);
            Assert.AreEqual(20, layout.MinColumnSpacing);
            Assert.AreEqual(20, layout.MinRowSpacing);
        }
        finally { await TestHost.UnloadAsync(items); }
    }
}
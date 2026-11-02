using DeviceRunners.UITesting;
using Routeables.Abstraction;
using Routeables.Maui;
using Xunit;

namespace Routeables.Test.Maui;

/// <summary><see cref="RouteableLink"/> on a real button, and <see cref="RouteableRegionItems"/> on a real layout.</summary>
public class LinkAndItemsDeviceTests
{
    private static void Click(Button button) => ((IButtonController)button).SendClicked();

    [UIFact]
    public async Task A_click_goes_to_the_route_in_the_default_region()
    {
        await Device.ResetAsync();
        var button = new Button();
        RouteableLink.SetRoute(button, "/b");
        using var host = new Hosted<Button>(button);

        Click(button);

        await Device.UntilAsync(() => Device.Router().CurrentView is PageBView);
    }

    [UIFact]
    public async Task A_click_goes_to_the_route_in_the_region_of_the_link()
    {
        await Device.ResetAsync();
        var button = new Button();
        RouteableLink.SetRoute(button, "/b");
        RouteableLink.SetRegion(button, MauiProgram.Side);
        using var host = new Hosted<Button>(button);

        Click(button);

        await Device.UntilAsync(() => Device.Router(MauiProgram.Side).CurrentView is PageBView);
        Assert.Null(Device.Router().CurrentView);
    }

    [UIFact]
    public async Task A_link_without_a_route_does_nothing()
    {
        await Device.ResetAsync();
        var button = new Button();
        RouteableLink.SetRoute(button, "/b");
        RouteableLink.SetRoute(button, null);
        using var host = new Hosted<Button>(button);

        Click(button);

        await Task.Delay(200);
        Assert.Null(Device.Router().CurrentView);
    }

    [UIFact]
    public async Task A_route_changed_several_times_is_followed_once_per_click()
    {
        await Device.ResetAsync();
        await Device.Services.GoToAsync("/a");
        var button = new Button();
        RouteableLink.SetRoute(button, "/a");
        RouteableLink.SetRoute(button, "/b");
        RouteableLink.SetRoute(button, "/b ");
        RouteableLink.SetRoute(button, "/b");
        using var host = new Hosted<Button>(button);

        Click(button);
        await Device.UntilAsync(() => Device.Router().CurrentView is PageBView);
        await Task.Delay(150);

        // One entry was added: Back is the first page again.
        await Device.Router().GoBackAsync();
        Assert.IsType<PageAView>(Device.Router().CurrentView);
    }

    [UIFact]
    public async Task A_route_nobody_registered_leaves_the_application_running_on_its_page()
    {
        await Device.ResetAsync();
        await Device.Services.GoToAsync("/a");
        var button = new Button();
        RouteableLink.SetRoute(button, "/nowhere");
        using var host = new Hosted<Button>(button);

        Click(button);

        await Task.Delay(300);
        Assert.IsType<PageAView>(Device.Router().CurrentView);
    }

    [Fact]
    public void The_attached_properties_are_read_back()
    {
        var button = new Button();

        RouteableLink.SetRoute(button, "/a");
        RouteableLink.SetRegion(button, "side");

        Assert.Equal("/a", RouteableLink.GetRoute(button));
        Assert.Equal("side", RouteableLink.GetRegion(button));
    }

    // ------------------------------------------------------------------------------------------------ RouteableRegionItems

    private static IReadOnlyList<ContentView> Holders(RouteableRegionItems items)
        => items.Content is ScrollView { Content: FlexLayout flex } ? [.. flex.Children.OfType<ContentView>()] : [];

    private static async Task<Hosted<RouteableRegionItems>> ItemsAsync(Action<RouteableRegionItems>? configure = null)
    {
        await Device.ResetAsync();
        var items = new RouteableRegionItems { Region = MauiProgram.Side };
        configure?.Invoke(items);
        return new Hosted<RouteableRegionItems>(items);
    }

    [UIFact]
    public async Task Every_view_of_the_history_is_shown_oldest_first()
    {
        using var host = await ItemsAsync();

        await Device.Services.GoToAsync("/a", MauiProgram.Side);
        await Device.Services.GoToAsync("/b", MauiProgram.Side);

        await Device.UntilAsync(() => Holders(host.View).Count == 2);
        var holders = Holders(host.View);
        Assert.IsType<PageAView>(holders[0].Content);
        Assert.IsType<PageBView>(holders[1].Content);
    }

    [UIFact]
    public async Task Going_back_removes_the_last_view()
    {
        using var host = await ItemsAsync();
        await Device.Services.GoToAsync("/a", MauiProgram.Side);
        await Device.Services.GoToAsync("/b", MauiProgram.Side);
        await Device.UntilAsync(() => Holders(host.View).Count == 2);

        await Device.Router(MauiProgram.Side).GoBackAsync();

        await Device.UntilAsync(() => Holders(host.View).Count == 1);
        Assert.IsType<PageAView>(Holders(host.View)[0].Content);
    }

    [UIFact]
    public async Task Erasing_the_history_leaves_no_view()
    {
        using var host = await ItemsAsync();
        await Device.Services.GoToAsync("/a", MauiProgram.Side);
        await Device.UntilAsync(() => Holders(host.View).Count == 1);

        await Device.Router(MauiProgram.Side).EraseHistoryAsync();

        await Device.UntilAsync(() => Holders(host.View).Count == 0);
    }

    [UIFact]
    public async Task The_spacing_and_the_width_are_given_to_each_view()
    {
        using var host = await ItemsAsync(i => { i.Spacing = 20; i.MinItemWidth = 150; });

        await Device.Services.GoToAsync("/a", MauiProgram.Side);

        await Device.UntilAsync(() => Holders(host.View).Count == 1);
        var holder = Holders(host.View)[0];
        Assert.Equal(150, holder.WidthRequest);
        Assert.Equal(new Thickness(10), holder.Padding);
    }

    [UIFact]
    public async Task A_control_that_leaves_the_screen_lets_go_of_its_views()
    {
        var host = await ItemsAsync();
        await Device.Services.GoToAsync("/a", MauiProgram.Side);
        await Device.UntilAsync(() => Holders(host.View).Count == 1);
        var page = Holders(host.View)[0].Content;

        host.Dispose();

        Assert.Null(host.View.Content);
        Assert.Null(((View)page!).Parent);
    }

    [UIFact]
    public async Task Views_pushed_again_after_the_control_came_back_are_all_shown()
    {
        var host = await ItemsAsync();
        await Device.Services.GoToAsync("/a", MauiProgram.Side);
        await Device.UntilAsync(() => Holders(host.View).Count == 1);
        var items = host.View;
        host.Dispose();

        Device.Attach(items);
        try
        {
            await Device.Services.GoToAsync("/b", MauiProgram.Side);
            await Device.UntilAsync(() => Holders(items).Count == 2);
        }
        finally
        {
            Device.Detach(items);
        }
    }
}

using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Threading;
using Routeables.Abstraction.Routing;
using Routeables.Avalonia;
using Xunit;

namespace Routeables.Test.Avalonia;

/// <summary>A <see cref="RouteableRegionItems"/> shows every view of the history of its region, and gives them up when it leaves the screen.</summary>
[Collection("Avalonia headless")]
public sealed class HeadlessRegionItemsTests(HeadlessApp app)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private Task RunAsync(Func<IServiceProvider, IRouter, RouteableRegionItems, Window, Task> test) => app.RunAsync(async () =>
    {
        var provider = app.Provider;
        var router = provider.GetRouter(HeadlessApp.Side);
        await router.EraseHistoryAsync();

        var items = new RouteableRegionItems { Region = HeadlessApp.Side };
        var window = new Window { Width = 400, Height = 300, Content = items };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            await test(provider, router, items, window);
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

    private static Type[] ViewTypes(RouteableRegionItems items) => [.. items.Items.Cast<object>().Select(view => view.GetType())];

    [Fact]
    public Task Every_view_of_the_history_is_shown_and_Back_removes_the_last() => RunAsync(async (provider, router, items, window) =>
    {
        await provider.GoToAsync("/a", HeadlessApp.Side);
        await provider.GoToAsync("/b", HeadlessApp.Side);
        await UntilAsync(() => items.ItemCount == 2);
        Assert.Equal([typeof(PageAView), typeof(PageBView)], ViewTypes(items));

        await router.GoBackAsync();
        await UntilAsync(() => items.ItemCount == 1);
        Assert.Equal([typeof(PageAView)], ViewTypes(items));
    });

    [Fact]
    public Task Another_region_shows_the_views_of_that_region() => RunAsync(async (provider, router, items, window) =>
    {
        await provider.GoToAsync("/a", HeadlessApp.Side);
        await UntilAsync(() => items.ItemCount == 1);

        items.Region = null;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(0, items.ItemCount);

        items.Region = HeadlessApp.Side;
        await UntilAsync(() => items.ItemCount == 1);
    });

    [Fact]
    public Task The_views_are_released_when_the_control_leaves_the_screen() => RunAsync(async (provider, router, items, window) =>
    {
        await provider.GoToAsync("/a", HeadlessApp.Side);
        await UntilAsync(() => items.ItemCount == 1);

        window.Content = null;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(0, items.ItemCount);
    });

    [Fact]
    public Task The_automation_peer_is_a_group_named_after_the_region() => RunAsync((provider, router, items, window) =>
    {
        var peer = ControlAutomationPeer.CreatePeerForElement(items);

        Assert.Equal(AutomationControlType.Group, peer.GetAutomationControlType());
        Assert.Equal(nameof(RouteableRegionItems), peer.GetClassName());
        Assert.Contains("RegionItems", peer.GetAutomationId());
        return Task.CompletedTask;
    });
}

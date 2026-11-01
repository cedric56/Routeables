using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Avalonia;
using Xunit;

namespace Routeables.Test.Avalonia;

/// <summary>
/// A window with two <see cref="RouteableRegion"/> (the default one and "side") and a <see cref="RouteableLink"/> button, driven through the
/// routers: the controls must show what the routers display, and follow Back and refusals.
/// </summary>
[Collection("Avalonia headless")]
public sealed class HeadlessRoutingTests(HeadlessApp app)
{
    private sealed class Screen : IAsyncDisposable
    {
        public RouteableRegion Main { get; } = new();
        public RouteableRegion Side { get; } = new() { Region = HeadlessApp.Side };
        public Button Link { get; } = new() { Content = "Go to B" };
        public Window Window { get; }

        public Screen(IServiceProvider provider)
        {
            RouteableLink.SetRoute(Link, "/b");
            RouteableLink.SetRegion(Link, HeadlessApp.Side);
            Window = new Window { Width = 400, Height = 300, Content = new StackPanel { Children = { Main, Side, Link } } };
            Window.Show();
            Dispatcher.UIThread.RunJobs();
        }

        public async ValueTask DisposeAsync()
        {
            Window.Close();
            await Task.CompletedTask;
        }
    }

    /// <summary>Starts from empty histories, shows the window, runs the test, closes the window.</summary>
    private Task RunAsync(Func<IServiceProvider, Screen, Task> test) => app.RunAsync(async () =>
    {
        var provider = app.Provider;
        await provider.GetRouter(null).EraseHistoryAsync();
        await provider.GetRouter(HeadlessApp.Side).EraseHistoryAsync();
        await using var screen = new Screen(provider);
        await test(provider, screen);
    });

    private static async Task UntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (true)
        {
            Dispatcher.UIThread.RunJobs();
            if (condition()) return;
            if (DateTime.UtcNow > deadline) throw new TimeoutException("The condition was not met in 5 seconds.");
            await Task.Delay(10);
        }
    }

    [Fact]
    public Task A_region_shows_the_view_of_the_route() => RunAsync(async (provider, screen) =>
    {
        var result = await provider.GoToAsync("/a");

        Assert.True(result.Success);
        await UntilAsync(() => screen.Main.Content is PageAView);
        var view = Assert.IsType<PageAView>(screen.Main.Content);
        Assert.IsType<PageAViewModel>(view.DataContext);
        Assert.Null(screen.Side.Content);
    });

    [Fact]
    public Task Going_back_shows_the_previous_view_again() => RunAsync(async (provider, screen) =>
    {
        await provider.GoToAsync("/a");
        await provider.GoToAsync("/b");
        await UntilAsync(() => screen.Main.Content is PageBView);

        var result = await provider.GetRouter(null).GoBackAsync();

        Assert.True(result.Success);
        await UntilAsync(() => screen.Main.Content is PageAView);
    });

    [Fact]
    public Task A_page_that_refuses_to_be_left_stays_on_screen() => RunAsync(async (provider, screen) =>
    {
        await provider.GoToAsync("/a");
        await UntilAsync(() => screen.Main.Content is PageAView);
        ((PageAViewModel)((PageAView)screen.Main.Content!).DataContext!).CanLeave = false;

        var refused = await provider.GoToAsync("/b");

        Assert.False(refused.Success);
        Dispatcher.UIThread.RunJobs();
        Assert.IsType<PageAView>(screen.Main.Content);

        ((PageAViewModel)((PageAView)screen.Main.Content!).DataContext!).CanLeave = true;
        Assert.True((await provider.GoToAsync("/b")).Success);
        await UntilAsync(() => screen.Main.Content is PageBView);
    });

    [Fact]
    public Task A_page_shown_again_while_its_transition_out_is_still_playing_is_released() => RunAsync(async (provider, screen) =>
    {
        // The control keeps the page it leaves in a second presenter until the animation ends: Back right away asks it to hold that page twice.
        screen.Main.PageTransition = new CrossFade(TimeSpan.FromSeconds(3));
        await provider.GoToAsync("/a");
        await UntilAsync(() => screen.Main.Content is PageAView);
        await provider.GoToAsync("/b");
        await UntilAsync(() => screen.Main.Content is PageBView);

        var back = await provider.GetRouter(null).GoBackAsync();

        Assert.True(back.Success);
        await UntilAsync(() => screen.Main.Content is PageAView);
    });

    [Fact]
    public Task Each_region_shows_its_own_view() => RunAsync(async (provider, screen) =>
    {
        await provider.GoToAsync("/a");
        await provider.GoToAsync("/b", HeadlessApp.Side);

        await UntilAsync(() => screen.Main.Content is PageAView && screen.Side.Content is PageBView);
    });

    [Fact]
    public Task A_RouteableLink_goes_to_its_route_in_its_region() => RunAsync(async (provider, screen) =>
    {
        await provider.GoToAsync("/a");
        await UntilAsync(() => screen.Main.Content is PageAView);

        screen.Link.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        await UntilAsync(() => screen.Side.Content is PageBView);
        Assert.IsType<PageAView>(screen.Main.Content);
    });
}

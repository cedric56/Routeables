using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
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
    /// <summary>A temporary window that hosts the main region, the side region and a routeable link button.</summary>
    private sealed class Screen : IAsyncDisposable
    {
        /// <summary>The default region under test.</summary>
        public RouteableRegion Main { get; } = new();

        /// <summary>The named side region under test.</summary>
        public RouteableRegion Side { get; } = new() { Region = HeadlessApp.Side };

        /// <summary>A button configured as a <see cref="RouteableLink"/> to "/b" in the side region.</summary>
        public Button Link { get; } = new() { Content = "Go to B" };

        /// <summary>The window that hosts the regions and the link.</summary>
        public Window Window { get; }

        /// <summary>Builds the window, configures the link and shows it on the UI thread.</summary>
        public Screen(IServiceProvider provider)
        {
            RouteableLink.SetRoute(Link, "/b");
            RouteableLink.SetRegion(Link, HeadlessApp.Side);
            Window = new Window { Width = 400, Height = 300, Content = new StackPanel { Children = { Main, Side, Link } } };
            Window.Show();
            Dispatcher.UIThread.RunJobs();
        }

        /// <summary>Closes the window.</summary>
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

    /// <summary>Polls <paramref name="condition"/> on the UI thread until it becomes true or a 5-second timeout elapses.</summary>
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

    /// <summary>Navigating to a route displays the corresponding view and view model in the default region.</summary>
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

    /// <summary>Going back restores the previous view in the default region.</summary>
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

    /// <summary>A page that refuses to be left stays on screen until it allows navigation.</summary>
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

    /// <summary>
    /// A page shown again while its transition-out animation is still playing is released correctly
    /// (the control keeps the leaving page in a second presenter until the animation ends).
    /// </summary>
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

    /// <summary>Each region displays the view belonging to its own navigation independently.</summary>
    [Fact]
    public Task Each_region_shows_its_own_view() => RunAsync(async (provider, screen) =>
    {
        await provider.GoToAsync("/a");
        await provider.GoToAsync("/b", HeadlessApp.Side);

        await UntilAsync(() => screen.Main.Content is PageAView && screen.Side.Content is PageBView);
    });

    /// <summary>Clicking a <see cref="RouteableLink"/> navigates to its route in its configured region.</summary>
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
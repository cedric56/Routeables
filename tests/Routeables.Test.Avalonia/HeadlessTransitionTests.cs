using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Threading;
using Routeables.Abstraction;
using Routeables.Avalonia;
using Routeables.Routing;
using Xunit;

namespace Routeables.Test.Avalonia;

/// <summary>
/// The transitions of a <see cref="RouteableRegion"/> in a real (headless) window: every built-in one, the one a navigation asks for, the custom
/// ones, and the sequences that used to break (Back while the page is still leaving, a page asked to be held twice, an unknown name).
/// Nothing here waits for an animation to end: what is checked is that the right page is shown, nothing throws, and the transition that
/// was chosen is the one given to the control.
/// </summary>
[Collection("Avalonia headless")]
public sealed class HeadlessTransitionTests(HeadlessApp app)
{
    /// <summary>A temporary window that hosts a single <see cref="RouteableRegion"/> under test.</summary>
    private sealed class Screen : IAsyncDisposable
    {
        /// <summary>The default region whose transitions are exercised.</summary>
        public RouteableRegion Main { get; } = new();

        /// <summary>The window that hosts the region.</summary>
        public Window Window { get; }

        /// <summary>Builds the window, hosts the region and shows it on the UI thread.</summary>
        public Screen()
        {
            Window = new Window { Width = 400, Height = 300, Content = new StackPanel { Children = { Main } } };
            Window.Show();
            Dispatcher.UIThread.RunJobs();
        }

        /// <summary>Closes the window.</summary>
        public ValueTask DisposeAsync()
        {
            Window.Close();
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>Starts from empty histories, shows the window, runs the test, closes the window.</summary>
    private Task RunAsync(Func<IServiceProvider, Screen, Task> test) => app.RunAsync(async () =>
    {
        var provider = app.Provider;
        await provider.GetRouter(null).EraseHistoryAsync();
        await provider.GetRouter(HeadlessApp.Side).EraseHistoryAsync();
        await using var screen = new Screen();
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

    /// <summary>Returns the default (unnamed) router from the provider.</summary>
    private static IRouter Default(IServiceProvider provider) => provider.GetRouter(null);

    /// <summary>Creates navigation arguments that request the given transition.</summary>
    private static IRouteableArgs With(RouteTransition transition) => RouteableArgs.Create().WithTransition(transition);

    /// <summary>Theory data containing the name of every built-in <see cref="RouteTransition"/>.</summary>
    public static TheoryData<string> BuiltInNames()
    {
        var data = new TheoryData<string>();
        foreach (var transition in RouteTransition.BuiltIn)
            data.Add(transition.Name);
        return data;
    }

    // ------------------------------------------------------------------------------------------------ built-in transitions

    /// <summary>Every built-in region transition shows pages correctly going forward and back.</summary>
    [Theory]
    [MemberData(nameof(BuiltInNames))]
    public Task Every_built_in_transition_of_the_region_shows_the_pages_going_forward_and_back(string name) => RunAsync(async (provider, screen) =>
    {
        screen.Main.Transition = name;
        await provider.GoToAsync("/a");
        await UntilAsync(() => screen.Main.Content is PageAView);
        await provider.GoToAsync("/b");
        await UntilAsync(() => screen.Main.Content is PageBView);
        await Default(provider).GoBackAsync();
        await UntilAsync(() => screen.Main.Content is PageAView);
        await Default(provider).ForwardAsync();
        await UntilAsync(() => screen.Main.Content is PageBView);
    });

    /// <summary>Every built-in transition requested by the navigation shows the target page.</summary>
    [Theory]
    [MemberData(nameof(BuiltInNames))]
    public Task Every_built_in_transition_asked_by_the_navigation_shows_the_page(string name) => RunAsync(async (provider, screen) =>
    {
        await provider.GoToAsync("/a", With(RouteTransition.Named(name)));
        await UntilAsync(() => screen.Main.Content is PageAView);
        await provider.GoToAsync("/b", With(RouteTransition.Named(name).WithDuration(TimeSpan.FromMilliseconds(80))));
        await UntilAsync(() => screen.Main.Content is PageBView);
    });

    /// <summary>Every built-in transition survives a rapid succession of navigations without losing the current view.</summary>
    [Theory]
    [MemberData(nameof(BuiltInNames))]
    public Task Every_built_in_transition_survives_a_quick_succession_of_changes(string name) => RunAsync(async (provider, screen) =>
    {
        screen.Main.Transition = name;
        for (var i = 0; i < 6; i++)
        {
            await provider.GoToAsync(i % 2 == 0 ? "/a" : "/b", With(RouteTransition.Named(name).WithDuration(TimeSpan.FromSeconds(2))));
            Dispatcher.UIThread.RunJobs();
        }
        await Default(provider).GoBackAsync();
        await Default(provider).GoBackAsync();
        await Default(provider).ForwardAsync();
        await UntilAsync(() => screen.Main.Content is PageAView or PageBView);
        Assert.True(Default(provider).CurrentView == screen.Main.Content);
    });

    /// <summary>A region with no transition shows the page immediately.</summary>
    [Fact]
    public Task A_region_without_transition_shows_the_page_at_once() => RunAsync(async (provider, screen) =>
    {
        await provider.GoToAsync("/a");
        await UntilAsync(() => screen.Main.Content is PageAView);
        await provider.GoToAsync("/b");
        await UntilAsync(() => screen.Main.Content is PageBView);
    });

    /// <summary>None on the region and None on the navigation still show the page.</summary>
    [Fact]
    public Task None_on_the_region_and_none_on_the_navigation_show_the_page() => RunAsync(async (provider, screen) =>
    {
        screen.Main.Transition = RouteTransition.None.Name;
        await provider.GoToAsync("/a");
        await UntilAsync(() => screen.Main.Content is PageAView);
        await provider.GoToAsync("/b", With(RouteTransition.None));
        await UntilAsync(() => screen.Main.Content is PageBView);
    });

    /// <summary>The transition requested by the navigation takes precedence; the next navigation falls back to the region's transition.</summary>
    [Fact]
    public Task The_transition_of_the_navigation_wins_and_the_next_navigation_goes_back_to_the_one_of_the_region() => RunAsync(async (provider, screen) =>
    {
        screen.Main.Transition = RouteTransition.Fade.Name;
        await provider.GoToAsync("/a", With(RouteTransition.Slide));
        await UntilAsync(() => screen.Main.Content is PageAView);
        await provider.GoToAsync("/b");
        await UntilAsync(() => screen.Main.Content is PageBView);
        await provider.GoToAsync("/a");
        await UntilAsync(() => screen.Main.Content is PageAView);
    });

    /// <summary>Changing the region's transition between navigations is taken into account for subsequent navigations.</summary>
    [Fact]
    public Task Changing_the_transition_of_the_region_between_two_navigations_is_taken_into_account() => RunAsync(async (provider, screen) =>
    {
        screen.Main.Transition = RouteTransition.Fade.Name;
        await provider.GoToAsync("/a");
        await UntilAsync(() => screen.Main.Content is PageAView);
        screen.Main.Transition = RouteTransition.Flip.Name;
        await provider.GoToAsync("/b");
        await UntilAsync(() => screen.Main.Content is PageBView);
        screen.Main.Transition = null;
        await provider.GoToAsync("/a");
        await UntilAsync(() => screen.Main.Content is PageAView);
    });

    // ------------------------------------------------------------------------------------------------ the transition of the Avalonia control

    /// <summary>A <see cref="RouteableRegion.Transition"/> set by the user is preserved when no named transition is declared.</summary>
    [Fact]
    public Task The_PageTransition_set_by_the_user_is_kept_when_no_transition_is_declared() => RunAsync(async (provider, screen) =>
    {
        var own = new CrossFade(TimeSpan.FromMilliseconds(50));
        screen.Main.PageTransition = own;
        await provider.GoToAsync("/a");
        await UntilAsync(() => screen.Main.Content is PageAView);
        await provider.GoToAsync("/b");
        await UntilAsync(() => screen.Main.Content is PageBView);
        Assert.Same(own, screen.Main.PageTransition);
    });

    // ------------------------------------------------------------------------------------------------ custom transitions

    /// <summary>A custom transition is built with the direction and duration of each navigation change.</summary>
    [Fact]
    public Task A_custom_transition_is_built_with_the_direction_and_the_duration_of_the_change() => RunAsync(async (provider, screen) =>
    {
        var seen = new List<(NavigationDirection Direction, TimeSpan Duration)>();
        RouteTransitions.Register("TestCustomSeen", (direction, duration) =>
        {
            seen.Add((direction, duration));
            return new CrossFade(duration);
        });
        screen.Main.Transition = RouteTransition.Named("TestCustomSeen").Name;
        var slow = With(RouteTransition.Named("TestCustomSeen").WithDuration(TimeSpan.FromMilliseconds(120)));
        await provider.GoToAsync("/a", slow);
        await UntilAsync(() => screen.Main.Content is PageAView);
        await provider.GoToAsync("/b", slow);
        await UntilAsync(() => screen.Main.Content is PageBView);
        await Default(provider).GoBackAsync();
        await UntilAsync(() => screen.Main.Content is PageAView);
        Assert.Contains(seen, s => s.Direction == NavigationDirection.Forward);
        Assert.Contains(seen, s => s.Direction == NavigationDirection.Back);
        Assert.All(seen.Where(s => s.Direction == NavigationDirection.Forward), s => Assert.Equal(TimeSpan.FromMilliseconds(120), s.Duration));
    });

    /// <summary>A custom transition requested by the navigation becomes the control's <see cref="RouteableRegion.Transition"/>.</summary>
    [Fact]
    public Task A_custom_transition_asked_by_the_navigation_becomes_the_transition_of_the_control() => RunAsync(async (provider, screen) =>
    {
        var made = new CrossFade(TimeSpan.FromMilliseconds(60));
        RouteTransitions.Register("TestCustomMade", (_, _) => made);
        await provider.GoToAsync("/a");
        await UntilAsync(() => screen.Main.Content is PageAView);
        await provider.GoToAsync("/b", With("TestCustomMade"));
        await UntilAsync(() => screen.Main.Content is PageBView);
        Assert.Same(made, screen.Main.PageTransition);
    });

    /// <summary>The user's original <see cref="RouteableRegion.Transition"/> is restored after a custom transition completes.</summary>
    [Fact]
    public Task The_PageTransition_of_the_user_comes_back_after_a_custom_transition() => RunAsync(async (provider, screen) =>
    {
        var own = new CrossFade(TimeSpan.FromMilliseconds(40));
        var made = new CrossFade(TimeSpan.FromMilliseconds(60));
        RouteTransitions.Register("TestCustomRestore", (_, _) => made);
        screen.Main.PageTransition = own;
        await provider.GoToAsync("/a");
        await UntilAsync(() => screen.Main.Content is PageAView);
        await provider.GoToAsync("/b", With("TestCustomRestore"));
        await UntilAsync(() => screen.Main.Content is PageBView);
        Assert.Same(made, screen.Main.PageTransition);
        await provider.GoToAsync("/a");
        await UntilAsync(() => screen.Main.Content is PageAView);
        Assert.Same(own, screen.Main.PageTransition);
    });

    /// <summary>A custom transition whose factory returns null still shows the page without throwing.</summary>
    [Fact]
    public Task A_custom_transition_that_builds_nothing_still_shows_the_page() => RunAsync(async (provider, screen) =>
    {
        RouteTransitions.Register("TestCustomNull", (_, _) => null);
        screen.Main.Transition = RouteTransition.Named("TestCustomNull").Name;
        await provider.GoToAsync("/a");
        await provider.GoToAsync("/b");
        await UntilAsync(() => screen.Main.Content is PageBView);
    });

    /// <summary>A custom transition name is resolved case-insensitively.</summary>
    [Fact]
    public Task A_custom_name_is_found_whatever_its_case() => RunAsync(async (provider, screen) =>
    {
        var calls = 0;
        RouteTransitions.Register("TestCustomCase", (_, d) => { calls++; return new CrossFade(d); });
        screen.Main.Transition = RouteTransition.Named("testcustomcase").Name;
        await provider.GoToAsync("/a");
        await UntilAsync(() => screen.Main.Content is PageAView);
        Assert.True(calls > 0);
    });

    /// <summary>Registering a transition without a valid name or without a factory is refused.</summary>
    [Fact]
    public void Registering_a_transition_without_a_name_or_a_factory_is_refused()
    {
        Assert.ThrowsAny<ArgumentException>(() => RouteTransitions.Register(" ", (_, _) => null));
        Assert.Throws<ArgumentNullException>(() => RouteTransitions.Register("TestCustomNoFactory", null!));
    }

    /// <summary>An unknown transition name shows the page immediately without failing.</summary>
    [Fact]
    public Task An_unknown_transition_name_shows_the_page_at_once_without_failing() => RunAsync(async (provider, screen) =>
    {
        screen.Main.Transition = RouteTransition.Named("NobodyKnowsThisOne").Name;
        await provider.GoToAsync("/a");
        await UntilAsync(() => screen.Main.Content is PageAView);
        await provider.GoToAsync("/b");
        await UntilAsync(() => screen.Main.Content is PageBView);
    });

    // ------------------------------------------------------------------------------------------------ sequences

    /// <summary>Going back while a page is still leaving shows the previous page correctly for the control's transition.</summary>
    [Fact]
    public Task Back_while_the_page_is_still_leaving_shows_the_previous_page_for_every_transition_of_the_control() => RunAsync(async (provider, screen) =>
    {
        screen.Main.PageTransition = new CrossFade(TimeSpan.FromSeconds(3));
        await provider.GoToAsync("/a");
        await UntilAsync(() => screen.Main.Content is PageAView);
        for (var i = 0; i < 4; i++)
        {
            await provider.GoToAsync("/b");
            await UntilAsync(() => screen.Main.Content is PageBView);
            Assert.True((await Default(provider).GoBackAsync()).Success);
            await UntilAsync(() => screen.Main.Content is PageAView);
        }
    });

    /// <summary>A region that declares a transition still shows a page that returns immediately after a long outgoing animation.</summary>
    [Fact]
    public Task A_region_that_declares_a_transition_shows_a_page_that_comes_back_at_once() => RunAsync(async (provider, screen) =>
    {
        screen.Main.Transition = RouteTransition.Slide.Name;
        await provider.GoToAsync("/a", With(RouteTransition.Slide.WithDuration(TimeSpan.FromSeconds(3))));
        await provider.GoToAsync("/b", With(RouteTransition.Slide.WithDuration(TimeSpan.FromSeconds(3))));
        await UntilAsync(() => screen.Main.Content is PageBView);
        Assert.True((await Default(provider).GoBackAsync()).Success);
        await UntilAsync(() => screen.Main.Content is PageAView);
        Assert.True((await Default(provider).ForwardAsync()).Success);
        await UntilAsync(() => screen.Main.Content is PageBView);
    });

    /// <summary>A refused navigation leaves the current page; the next accepted navigation plays normally.</summary>
    [Fact]
    public Task A_refused_navigation_leaves_the_page_and_the_next_accepted_one_plays_normally() => RunAsync(async (provider, screen) =>
    {
        screen.Main.Transition = RouteTransition.Fade.Name;
        await provider.GoToAsync("/a");
        await UntilAsync(() => screen.Main.Content is PageAView);
        var model = (PageAViewModel)((PageAView)screen.Main.Content!).DataContext!;
        model.CanLeave = false;
        Assert.False((await provider.GoToAsync("/b", With(RouteTransition.Flip))).Success);
        Dispatcher.UIThread.RunJobs();
        Assert.IsType<PageAView>(screen.Main.Content);
        model.CanLeave = true;
        Assert.True((await provider.GoToAsync("/b")).Success);
        await UntilAsync(() => screen.Main.Content is PageBView);
    });

    /// <summary>Erasing the history empties the region regardless of the active transition.</summary>
    [Fact]
    public Task Erasing_the_history_empties_the_region_whatever_the_transition() => RunAsync(async (provider, screen) =>
    {
        screen.Main.Transition = RouteTransition.Curtain.Name;
        await provider.GoToAsync("/a");
        await UntilAsync(() => screen.Main.Content is PageAView);
        await Default(provider).EraseHistoryAsync();
        await UntilAsync(() => screen.Main.Content is null);
    });

    /// <summary>Requesting a page again while the region's transition is still playing does not throw.</summary>
    [Fact]
    public Task A_page_asked_again_while_the_transition_of_the_region_is_playing_does_not_throw() => RunAsync(async (provider, screen) =>
    {
        screen.Main.Transition = RouteTransition.Rotate.Name;
        for (var i = 0; i < 5; i++)
        {
            await provider.GoToAsync("/a", With(RouteTransition.Rotate.WithDuration(TimeSpan.FromSeconds(3))));
            await provider.GoToAsync("/b", With(RouteTransition.Rotate.WithDuration(TimeSpan.FromSeconds(3))));
        }
        await UntilAsync(() => screen.Main.Content is PageBView);
    });

    /// <summary>Closing a window while a transition is playing does not break a subsequent screen on the same application.</summary>
    [Fact]
    public async Task A_window_closed_while_a_transition_is_playing_does_not_break_the_next_screen() => await app.RunAsync(async () =>
    {
        var provider = app.Provider;
        await Default(provider).EraseHistoryAsync();
        var first = new Screen();
        first.Main.Transition = RouteTransition.Slide.Name;
        await provider.GoToAsync("/a", With(RouteTransition.Slide.WithDuration(TimeSpan.FromSeconds(3))));
        await provider.GoToAsync("/b", With(RouteTransition.Slide.WithDuration(TimeSpan.FromSeconds(3))));
        await first.DisposeAsync();
        Dispatcher.UIThread.RunJobs();
        await using var second = new Screen();
        await provider.GoToAsync("/a");
        await UntilAsync(() => second.Main.Content is PageAView);
    });

    /// <summary>Two regions with different transitions operate independently without interfering with each other.</summary>
    [Fact]
    public Task Two_regions_with_different_transitions_do_not_disturb_each_other() => RunAsync(async (provider, screen) =>
    {
        var side = new RouteableRegion { Region = HeadlessApp.Side, Transition = RouteTransition.Pop.Name };
        ((StackPanel)screen.Window.Content!).Children.Add(side);
        screen.Main.Transition = RouteTransition.Slide.Name;
        Dispatcher.UIThread.RunJobs();
        await provider.GoToAsync("/a");
        await provider.GoToAsync("/b", HeadlessApp.Side);
        await provider.GoToAsync("/b");
        await UntilAsync(() => screen.Main.Content is PageBView && side.Content is PageBView);
        await Default(provider).GoBackAsync();
        await UntilAsync(() => screen.Main.Content is PageAView && side.Content is PageBView);
    });
}
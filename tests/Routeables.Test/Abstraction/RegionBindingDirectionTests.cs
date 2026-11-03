using Routeables.Abstraction;
using Routeables.Regions;
using Routeables.Routing;
using Routeables.Test.Abstraction;
using static Routeables.Test.Abstraction.RouterTestKit;
using Page = Routeables.Test.Abstraction.Page;

namespace Routeables.Tests.Abstraction;

/// <summary>
/// What a region host is told about each route change of its router, through the real <see cref="Router"/>: the view, the direction the history
/// moved and the transition of the navigation. A refused, failed or cancelled change must show nothing and leave nothing behind for the next one.
/// </summary>
public class RegionBindingDirectionTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class Shown
    {
        public List<(object? View, RegionChange Change)> Calls { get; } = [];

        public (object? View, RegionChange Change) Last => Calls[^1];

        public void Add(object? view, RegionChange change) => Calls.Add((view, change));
    }

    private static (Router Router, RegionBinding Binding, Shown Shown, CallLog Log) Start()
    {
        var router = CreateRouter();
        var shown = new Shown();
        var binding = new RegionBinding(router, shown.Add);
        return (router, binding, shown, new CallLog());
    }

    private static IRouteableArgs With(RouteTransition transition) => RouteableArgs.Create().WithTransition(transition);

    private static Task<RouteableResult> Go(Router router, Page page, IRouteableArgs? args = null)
        => router.GoAsync(page.View, page.ViewModel, args, Ct);

    private static Page NewPage(string name, CallLog log)
        => new(new RecordingViewModel($"vm{name}", log), new RecordingView($"view{name}", log));

    // ------------------------------------------------------------------------------------------------ directions

    [Fact]
    public void A_region_that_starts_on_an_empty_router_shows_nothing_with_an_unknown_change()
    {
        var (_, binding, shown, _) = Start();
        using (binding)
        {
            var call = Assert.Single(shown.Calls);
            Assert.Null(call.View);
            Assert.Equal(RegionChange.Unknown, call.Change);
        }
    }

    [Fact]
    public async Task A_region_that_starts_on_a_router_that_already_routed_shows_that_view_with_an_unknown_change()
    {
        var router = CreateRouter();
        var log = new CallLog();
        var page = await GoToAsync(router, "A", log);
        var shown = new Shown();

        using var binding = new RegionBinding(router, shown.Add);

        Assert.Same(page.View, Assert.Single(shown.Calls).View);
        Assert.Equal(RegionChange.Unknown, shown.Last.Change);
    }

    [Fact]
    public async Task A_push_moves_forward()
    {
        var (router, binding, shown, log) = Start();
        using var _ = binding;

        var page = await GoToAsync(router, "A", log);

        Assert.Same(page.View, shown.Last.View);
        Assert.Equal(NavigationDirection.Forward, shown.Last.Change.Direction);
        Assert.Null(shown.Last.Change.Transition);
    }

    [Fact]
    public async Task Back_moves_back_and_Forward_moves_forward()
    {
        var (router, binding, shown, log) = Start();
        using var _ = binding;
        var pages = await GoToAllAsync(router, log, "A", "B");

        await router.GoBackAsync(Ct);
        Assert.Same(pages[0].View, shown.Last.View);
        Assert.Equal(NavigationDirection.Back, shown.Last.Change.Direction);

        await router.ForwardAsync(Ct);
        Assert.Same(pages[1].View, shown.Last.View);
        Assert.Equal(NavigationDirection.Forward, shown.Last.Change.Direction);
    }

    [Fact]
    public async Task Pop_moves_back()
    {
        var (router, binding, shown, log) = Start();
        using var _ = binding;
        var pages = await GoToAllAsync(router, log, "A", "B");

        await router.PopAsync(Ct);

        Assert.Same(pages[0].View, shown.Last.View);
        Assert.Equal(NavigationDirection.Back, shown.Last.Change.Direction);
    }

    [Fact]
    public async Task Going_back_to_the_root_moves_back_and_shows_the_first_page()
    {
        var (router, binding, shown, log) = Start();
        using var _ = binding;
        var pages = await GoToAllAsync(router, log, "A", "B", "C");

        await router.GoBackToRootAsync(Ct);

        Assert.Same(pages[0].View, shown.Last.View);
        Assert.Equal(NavigationDirection.Back, shown.Last.Change.Direction);
    }

    [Fact]
    public async Task MoveTo_a_later_entry_moves_forward_and_an_earlier_one_moves_back()
    {
        var (router, binding, shown, log) = Start();
        using var _ = binding;
        var pages = await GoToAllAsync(router, log, "A", "B", "C");
        await router.MoveToAsync(0, Ct);
        Assert.Same(pages[0].View, shown.Last.View);
        Assert.Equal(NavigationDirection.Back, shown.Last.Change.Direction);

        await router.MoveToAsync(2, Ct);

        Assert.Same(pages[2].View, shown.Last.View);
        Assert.Equal(NavigationDirection.Forward, shown.Last.Change.Direction);
    }

    [Fact]
    public async Task MoveTo_the_current_entry_shows_nothing_new()
    {
        var (router, binding, shown, log) = Start();
        using var _ = binding;
        await GoToAllAsync(router, log, "A", "B");
        var before = shown.Calls.Count;

        await router.MoveToAsync(1, Ct);

        Assert.Equal(before, shown.Calls.Count);
    }

    [Fact]
    public async Task Erasing_the_history_shows_nothing_with_an_unknown_change()
    {
        var (router, binding, shown, log) = Start();
        using var _ = binding;
        await GoToAllAsync(router, log, "A", "B");

        await router.EraseHistoryAsync(Ct);

        Assert.Null(shown.Last.View);
        Assert.Equal(RegionChange.Unknown, shown.Last.Change);
    }

    [Fact]
    public async Task A_page_shown_after_the_history_was_erased_moves_forward_again()
    {
        var (router, binding, shown, log) = Start();
        using var _ = binding;
        await GoToAllAsync(router, log, "A", "B");
        await router.EraseHistoryAsync(Ct);

        var page = await GoToAsync(router, "C", log);

        Assert.Same(page.View, shown.Last.View);
        Assert.Equal(NavigationDirection.Forward, shown.Last.Change.Direction);
    }

    // ------------------------------------------------------------------------------------------------ the transition of the navigation

    [Fact]
    public async Task The_transition_of_the_navigation_reaches_the_region()
    {
        var (router, binding, shown, log) = Start();
        using var _ = binding;

        await GoToAsync(router, "A", log, With(RouteTransition.Slide));

        Assert.Same(RouteTransition.Slide, shown.Last.Change.Transition);
    }

    [Fact]
    public async Task The_duration_of_the_transition_of_the_navigation_reaches_the_region()
    {
        var (router, binding, shown, log) = Start();
        using var _ = binding;

        await GoToAsync(router, "A", log, With(RouteTransition.Fade.WithDuration(TimeSpan.FromMilliseconds(700))));

        Assert.Equal(TimeSpan.FromMilliseconds(700), shown.Last.Change.Transition!.Duration);
    }

    [Fact]
    public async Task The_transition_of_a_navigation_does_not_leak_into_the_next_one()
    {
        var (router, binding, shown, log) = Start();
        using var _ = binding;
        await GoToAsync(router, "A", log, With(RouteTransition.Slide));

        await GoToAsync(router, "B", log);

        Assert.Null(shown.Last.Change.Transition);
    }

    [Fact]
    public async Task A_transition_of_none_is_carried_as_none_and_not_dropped()
    {
        var (router, binding, shown, log) = Start();
        using var _ = binding;

        await GoToAsync(router, "A", log, With(RouteTransition.None));

        Assert.Same(RouteTransition.None, shown.Last.Change.Transition);
    }

    [Fact]
    public async Task Going_back_plays_the_transition_the_entry_it_returns_to_was_pushed_with_and_going_forward_the_one_of_the_entry_it_returns_to()
    {
        // The change of a history move takes the arguments of the entry that becomes current. This pins that behaviour down.
        var (router, binding, shown, log) = Start();
        using var _ = binding;
        await GoToAsync(router, "A", log);
        await GoToAsync(router, "B", log, With(RouteTransition.Slide));

        await router.GoBackAsync(Ct);
        Assert.Null(shown.Last.Change.Transition);
        Assert.Equal(NavigationDirection.Back, shown.Last.Change.Direction);

        await router.ForwardAsync(Ct);
        Assert.Same(RouteTransition.Slide, shown.Last.Change.Transition);
        Assert.Equal(NavigationDirection.Forward, shown.Last.Change.Direction);
    }

    // ------------------------------------------------------------------------------------------------ refused, failed, cancelled

    [Fact]
    public async Task A_page_that_refuses_to_be_left_shows_nothing_new()
    {
        var (router, binding, shown, log) = Start();
        using var _ = binding;
        var a = await GoToAsync(router, "A", log);
        a.ViewModel.CanLeave = false;
        var before = shown.Calls.Count;

        var result = await Go(router, NewPage("B", log), With(RouteTransition.Slide));

        Assert.False(result.Success);
        Assert.Equal(before, shown.Calls.Count);
        Assert.Same(a.View, shown.Last.View);
    }

    [Fact]
    public async Task The_transition_of_a_refused_navigation_does_not_reach_the_next_one()
    {
        var (router, binding, shown, log) = Start();
        using var _ = binding;
        var a = await GoToAsync(router, "A", log);
        a.ViewModel.CanLeave = false;
        await Go(router, NewPage("B", log), With(RouteTransition.Slide));
        a.ViewModel.CanLeave = true;

        await GoToAsync(router, "C", log);

        Assert.Null(shown.Last.Change.Transition);
        Assert.Equal(NavigationDirection.Forward, shown.Last.Change.Direction);
    }

    [Fact]
    public async Task A_refused_back_shows_nothing_and_the_next_back_still_moves_back()
    {
        var (router, binding, shown, log) = Start();
        using var _ = binding;
        var pages = await GoToAllAsync(router, log, "A", "B");
        pages[1].ViewModel.CanLeave = false;
        var before = shown.Calls.Count;

        await router.GoBackAsync(Ct);
        Assert.Equal(before, shown.Calls.Count);

        pages[1].ViewModel.CanLeave = true;
        await router.GoBackAsync(Ct);
        Assert.Same(pages[0].View, shown.Last.View);
        Assert.Equal(NavigationDirection.Back, shown.Last.Change.Direction);
    }

    [Fact]
    public async Task A_page_that_fails_to_enter_shows_nothing_and_the_next_page_has_its_own_change()
    {
        var (router, binding, shown, log) = Start();
        using var _ = binding;
        var a = await GoToAsync(router, "A", log);
        var broken = NewPage("Broken", log);
        broken.ViewModel.EnterFails = new InvalidOperationException("enter failed");
        var before = shown.Calls.Count;

        await Assert.ThrowsAsync<InvalidOperationException>(() => Go(router, broken, With(RouteTransition.Slide)));
        Assert.Equal(before, shown.Calls.Count);
        Assert.Same(a.View, router.CurrentView);

        await GoToAsync(router, "B", log);

        Assert.Null(shown.Last.Change.Transition);
        Assert.Equal(NavigationDirection.Forward, shown.Last.Change.Direction);
    }

    [Fact]
    public async Task A_page_that_fails_to_initialize_shows_nothing()
    {
        var (router, binding, shown, log) = Start();
        using var _ = binding;
        await GoToAsync(router, "A", log);
        var broken = NewPage("Broken", log);
        broken.ViewModel.InitializeFails = new InvalidOperationException("init failed");
        var before = shown.Calls.Count;

        await Assert.ThrowsAsync<InvalidOperationException>(() => Go(router, broken));

        Assert.Equal(before, shown.Calls.Count);
    }

    [Fact]
    public async Task A_navigation_cancelled_while_a_page_is_entering_shows_nothing_and_leaves_no_transition_behind()
    {
        var (router, binding, shown, log) = Start();
        using var _ = binding;
        await GoToAsync(router, "A", log);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var slow = NewPage("Slow", log);
        slow.ViewModel.OnEnter = token => Task.Delay(Timeout, token);
        var before = shown.Calls.Count;

        var route = Task.Run(() => router.GoAsync(slow.View, slow.ViewModel, With(RouteTransition.Slide), cts.Token), Ct);
        while (!log.Entries.Contains("vmSlow:To"))
            await Task.Delay(10, Ct);
        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => route.WaitAsync(Timeout, Ct));
        Assert.Equal(before, shown.Calls.Count);

        await GoToAsync(router, "B", log);

        Assert.Null(shown.Last.Change.Transition);
    }

    // ------------------------------------------------------------------------------------------------ several hosts, several routers

    [Fact]
    public async Task Two_hosts_of_one_router_are_told_the_same_thing()
    {
        var router = CreateRouter();
        var first = new Shown();
        var second = new Shown();
        using var a = new RegionBinding(router, first.Add);
        using var b = new RegionBinding(router, second.Add);

        await GoToAsync(router, "A", new CallLog(), With(RouteTransition.SlideUp));

        Assert.Same(first.Last.View, second.Last.View);
        Assert.Equal(first.Last.Change, second.Last.Change);
        Assert.Same(RouteTransition.SlideUp, second.Last.Change.Transition);
    }

    [Fact]
    public async Task Two_routers_do_not_tell_each_other_s_hosts_anything()
    {
        var left = Start();
        var right = Start();
        using var l = left.Binding;
        using var r = right.Binding;
        var rightBefore = right.Shown.Calls.Count;

        await GoToAsync(left.Router, "A", left.Log, With(RouteTransition.Slide));

        Assert.Equal(rightBefore, right.Shown.Calls.Count);
    }

    [Fact]
    public async Task A_host_removed_from_a_router_stops_being_told_and_the_other_one_goes_on()
    {
        var router = CreateRouter();
        var kept = new Shown();
        var removed = new Shown();
        using var a = new RegionBinding(router, kept.Add);
        var b = new RegionBinding(router, removed.Add);
        var log = new CallLog();
        await GoToAsync(router, "A", log);
        var removedBefore = removed.Calls.Count;

        b.Dispose();
        await GoToAsync(router, "B", log);

        Assert.Equal(removedBefore, removed.Calls.Count);
        Assert.Equal(2, kept.Calls.Count(c => c.View is not null));
    }

    [Fact]
    public async Task Disposing_a_host_twice_is_harmless_and_a_disposed_host_is_not_refreshed()
    {
        var (router, binding, shown, log) = Start();
        await GoToAsync(router, "A", log);
        var before = shown.Calls.Count;

        binding.Dispose();
        binding.Dispose();
        binding.Refresh();
        await GoToAsync(router, "B", log);

        Assert.Equal(before, shown.Calls.Count);
    }

    // ------------------------------------------------------------------------------------------------ refresh and order

    [Fact]
    public async Task Refresh_shows_nothing_when_the_view_has_not_changed()
    {
        var (router, binding, shown, log) = Start();
        using var _ = binding;
        await GoToAsync(router, "A", log);
        var before = shown.Calls.Count;

        binding.Refresh();
        binding.Refresh();

        Assert.Equal(before, shown.Calls.Count);
    }

    [Fact]
    public async Task Twenty_pushes_in_a_row_are_shown_in_order_each_moving_forward()
    {
        var (router, binding, shown, log) = Start();
        using var _ = binding;

        var pages = new List<Page>();
        for (var i = 0; i < 20; i++)
            pages.Add(await GoToAsync(router, $"P{i}", log));

        var views = shown.Calls.Skip(1).Select(c => c.View!).ToList();
        Assert.Equal(pages.Select(p => (object)p.View), views);
        Assert.All(shown.Calls.Skip(1), c => Assert.Equal(NavigationDirection.Forward, c.Change.Direction));
    }

    [Fact]
    public async Task Back_and_forward_in_a_row_alternate_the_direction()
    {
        var (router, binding, shown, log) = Start();
        using var _ = binding;
        await GoToAllAsync(router, log, "A", "B");

        for (var i = 0; i < 5; i++)
        {
            await router.GoBackAsync(Ct);
            Assert.Equal(NavigationDirection.Back, shown.Last.Change.Direction);
            await router.ForwardAsync(Ct);
            Assert.Equal(NavigationDirection.Forward, shown.Last.Change.Direction);
        }
    }

    [Fact]
    public async Task Pages_routed_together_from_several_tasks_are_all_shown_and_each_has_a_known_direction()
    {
        var (router, binding, shown, log) = Start();
        using var _ = binding;

        await Task.WhenAll(Enumerable.Range(0, 10).Select(i => Task.Run(() => GoToAsync(router, $"P{i}", log), Ct)))
            .WaitAsync(TimeSpan.FromSeconds(20), Ct);

        Assert.Equal(10, shown.Calls.Count(c => c.View is not null));
        Assert.All(shown.Calls.Skip(1), c => Assert.Equal(NavigationDirection.Forward, c.Change.Direction));
    }

    [Fact]
    public async Task A_host_that_shows_with_the_one_argument_callback_still_follows_the_router()
    {
        var router = CreateRouter();
        var views = new List<object?>();
        using var binding = new RegionBinding(router, views.Add);

        var page = await GoToAsync(router, "A", new CallLog());

        Assert.Same(page.View, views[^1]);
    }
}

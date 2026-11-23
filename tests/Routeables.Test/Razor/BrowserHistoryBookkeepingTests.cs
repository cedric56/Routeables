using NSubstitute;
using Routeables.Abstraction.History;
using Routeables.Abstraction.Routing;
using Routeables.Blazor;

namespace Routeables.Tests.Razor;

/// <summary>The two small parts of <see cref="RouteableUrlSync"/> that count the browser entries and name them.</summary>
public class BrowserHistoryBookkeepingTests
{
    [Fact]
    public void A_push_adds_an_entry_behind_and_forgets_the_ones_ahead()
    {
        var depth = new BrowserHistoryDepth();
        depth.Track(RouteableKind.Push);
        depth.Track(RouteableKind.Push);
        depth.Track(RouteableKind.Back);
        Assert.Equal((1, 1), (depth.Back, depth.Forward));

        depth.Track(RouteableKind.Push);

        Assert.Equal((2, 0), (depth.Back, depth.Forward));
    }

    [Fact]
    public void A_back_and_a_forward_move_one_entry_from_one_side_to_the_other_and_stop_at_zero()
    {
        var depth = new BrowserHistoryDepth();
        depth.Track(RouteableKind.Back); // nothing behind
        depth.Track(RouteableKind.Forward); // nothing ahead
        Assert.Equal((0, 0), (depth.Back, depth.Forward));

        depth.Track(RouteableKind.Push);
        depth.Track(RouteableKind.Back);
        Assert.Equal((0, 1), (depth.Back, depth.Forward));
        depth.Track(RouteableKind.Forward);
        Assert.Equal((1, 0), (depth.Back, depth.Forward));
    }

    [Fact]
    public void A_pop_goes_back_and_forgets_what_is_ahead_while_any_other_move_starts_again()
    {
        var depth = new BrowserHistoryDepth();
        depth.Track(RouteableKind.Push);
        depth.Track(RouteableKind.Push);
        depth.Track(RouteableKind.Back);
        depth.Track(RouteableKind.Pop);
        Assert.Equal((0, 0), (depth.Back, depth.Forward));

        depth.Track(RouteableKind.Push);
        depth.Track(RouteableKind.Root);
        Assert.Equal((0, 0), (depth.Back, depth.Forward));
    }

    [Fact]
    public void The_browser_follows_a_move_only_when_it_holds_the_entry()
    {
        var depth = new BrowserHistoryDepth();
        Assert.False(depth.CanFollow(RouteableKind.Back));
        Assert.False(depth.CanFollow(RouteableKind.Forward));

        depth.Track(RouteableKind.Push);
        Assert.True(depth.CanFollow(RouteableKind.Back));
        Assert.True(depth.CanFollow(RouteableKind.Pop));
        Assert.False(depth.CanFollow(RouteableKind.Forward));
        Assert.False(depth.CanFollow(RouteableKind.Push));

        depth.Track(RouteableKind.Back);
        Assert.True(depth.CanFollow(RouteableKind.Forward));
        depth.Reset();
        Assert.False(depth.CanFollow(RouteableKind.Forward));
    }

    [Fact]
    public void An_entry_keeps_its_state_and_each_entry_has_its_own()
    {
        var states = new BrowserEntryStates();
        var home = Substitute.For<IHistoryEntry>();
        var detail = Substitute.For<IHistoryEntry>();

        Assert.Equal(states.StateOf(home), states.StateOf(home));
        Assert.NotEqual(states.StateOf(home), states.StateOf(detail));
    }

    [Fact]
    public void A_state_names_the_entry_it_was_given_to_and_nothing_else()
    {
        var states = new BrowserEntryStates();
        var home = Substitute.For<IHistoryEntry>();
        var detail = Substitute.For<IHistoryEntry>();
        var unknown = Substitute.For<IHistoryEntry>();
        var detailState = states.StateOf(detail);
        states.StateOf(home);
        IReadOnlyList<IHistoryEntry> entries = [home, detail];

        Assert.Equal(1, states.IndexOf(detailState, entries));
        Assert.Equal(-1, states.IndexOf(null, entries));
        Assert.Equal(-1, states.IndexOf("another application's state", entries));
        Assert.Equal(-1, states.IndexOf("routeables:ffffffff:1", entries)); // a state of a previous run
        Assert.Equal(-1, states.IndexOf(states.StateOf(unknown), entries)); // an entry the router no longer holds
    }

    [Fact]
    public void The_states_of_two_services_never_meet()
    {
        var first = new BrowserEntryStates();
        var second = new BrowserEntryStates();
        var entry = Substitute.For<IHistoryEntry>();

        var state = first.StateOf(entry);

        Assert.Equal(-1, second.IndexOf(state, [entry])); // the second never gave this entry a state
    }
}

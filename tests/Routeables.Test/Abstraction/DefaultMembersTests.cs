using Routeables.Abstraction;
using Routeables.Abstraction.History;
using Routeables.Abstraction.Registrations;
using Routeables.Abstraction.Routing;
using Routeables.Generated;

namespace Routeables.Tests.Abstraction;

/// <summary>
/// The members that an interface implements by default are only run by a class that does not write its own: these tests use
/// the smallest implementations, so what the interfaces do for any router or any arguments is checked.
/// </summary>
public class DefaultMembersTests
{
    private sealed class Entry(string name) : IHistoryEntry
    {
        public object ViewModel { get; } = name;
        public object View { get; } = name;
        public IRouteableArgs Args { get; } = new MinimalArgs();
    }

    private sealed class MinimalHistory(int current, params string[] names) : IRouterHistory
    {
        private readonly List<IHistoryEntry> _entries = names.Select(n => (IHistoryEntry)new Entry(n)).ToList();
        public int Count => _entries.Count;
        public int? MaxSize => null;
        public int CurrentIndex { get; } = current;
        public bool CanGoBack => CurrentIndex > 0;
        public bool CanGoForward => CurrentIndex < Count - 1;
        public IHistoryEntry? Root => _entries.FirstOrDefault();
        public IHistoryEntry? Current => CurrentIndex >= 0 && CurrentIndex < Count ? _entries[CurrentIndex] : null;
        public IReadOnlyList<IHistoryEntry> Entries => _entries;
        public event EventHandler? Changed { add { } remove { } }
    }

    private sealed class MinimalRouter(IRouterHistory history) : IRouter
    {
        public List<int> MovedTo { get; } = [];
        public IRouterHistory History { get; } = history;
        public event EventHandler<RouteableRoutingEventArgs>? Routing { add { } remove { } }
        public event EventHandler<RouteableRoutedEventArgs>? Routed { add { } remove { } }
        public bool CanGoBack => History.CanGoBack;
        public bool CanGoForward => History.CanGoForward;
        public object? CurrentViewModel => History.Current?.ViewModel;
        public object? CurrentView => History.Current?.View;
        public Task<RouteableResult> GoBackAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<RouteableResult> ForwardAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<RouteableResult> GoBackToRootAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<RouteableResult> PopAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<RouteableResult> MoveToAsync(int index, CancellationToken cancellationToken = default)
        {
            MovedTo.Add(index);
            return Task.FromResult(new RouteableResult());
        }
        public Task EraseHistoryAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<RouteableResult> GoToAsync(string url, IRouteableArgs? args = null, string? template = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<RouteableResult> GoAsync(object view, object viewModel, IRouteableArgs? args = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<RouteableResult> GoToRouteAsync(RouteableRouteRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class MinimalArgs : IRouteableArgs
    {
        public List<RouteableArgument> ViewModelParameters { get; } = [];
        public List<RouteableArgument> ViewParameters { get; } = [];
        public List<RouteableArgument> Values { get; } = [];
        public Exception? Exception { get; set; }
        public string? Route { get; set; }
        public IRouteableArgs ForViewModel(params RouteableArgument[] entries) { ViewModelParameters.AddRange(entries); return this; }
        public IRouteableArgs ForValues(params RouteableArgument[] entries) { Values.AddRange(entries); return this; }
        public IRouteableArgs ForView(params RouteableArgument[] entries) { ViewParameters.AddRange(entries); return this; }
        public IRouteableArgs WithRoute(string? route = null) { Route = route; return this; }
    }

    // ---- IRouter

    [Fact]
    public async Task MoveBack_by_a_match_goes_to_the_nearest_earlier_entry_that_satisfies_it()
    {
        IRouter router = new MinimalRouter(new MinimalHistory(3, "a", "b", "a", "c"));

        var result = await router.MoveBackAsync(e => (string)e.ViewModel == "a", TestContext.Current.CancellationToken);

        Assert.False(result.Cancelled);
        Assert.Equal(new[] { 2 }, ((MinimalRouter)router).MovedTo);
    }

    [Fact]
    public async Task MoveBack_by_a_match_is_cancelled_when_no_entry_satisfies_it()
    {
        IRouter router = new MinimalRouter(new MinimalHistory(2, "a", "b", "c"));

        var result = await router.MoveBackAsync(e => false, TestContext.Current.CancellationToken);

        Assert.True(result.Cancelled);
        Assert.Empty(((MinimalRouter)router).MovedTo);
    }

    [Fact]
    public async Task MoveBack_by_a_match_refuses_a_missing_predicate()
    {
        IRouter router = new MinimalRouter(new MinimalHistory(0, "a"));

        await Assert.ThrowsAsync<ArgumentNullException>(() => router.MoveBackAsync((Func<IHistoryEntry, bool>)null!, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MoveBack_by_steps_moves_that_many_entries_back()
    {
        IRouter router = new MinimalRouter(new MinimalHistory(3, "a", "b", "c", "d"));

        var result = await router.MoveBackAsync(2, TestContext.Current.CancellationToken);

        Assert.False(result.Cancelled);
        Assert.Equal(new[] { 1 }, ((MinimalRouter)router).MovedTo);
    }

    [Fact]
    public async Task MoveBack_by_steps_is_cancelled_when_the_history_is_shorter()
    {
        IRouter router = new MinimalRouter(new MinimalHistory(1, "a", "b"));

        var result = await router.MoveBackAsync(2, TestContext.Current.CancellationToken);

        Assert.True(result.Cancelled);
        Assert.Empty(((MinimalRouter)router).MovedTo);
    }

    [Fact]
    public async Task MoveBack_by_steps_refuses_less_than_one_step()
    {
        IRouter router = new MinimalRouter(new MinimalHistory(1, "a", "b"));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => router.MoveBackAsync(0, cancellationToken: TestContext.Current.CancellationToken));
    }

    // ---- IRouteableArgs

    [Fact]
    public void The_default_region_and_transition_are_none_and_cannot_be_set()
    {
        IRouteableArgs args = new MinimalArgs();

        Assert.Null(args.Router);
        Assert.Null(args.Region);
        Assert.Null(args.Transition);
        args.Router = null;
        args.Region = "side";
        args.Transition = null;
        Assert.Null(args.Region);
    }

    [Fact]
    public void WithRegion_and_WithTransition_return_the_same_arguments()
    {
        IRouteableArgs args = new MinimalArgs();

        Assert.Same(args, args.WithRegion("side"));
        Assert.Same(args, args.WithTransition(null));
    }

    [Fact]
    public void Positional_values_become_entries_without_a_name_and_entries_are_kept_as_they_are()
    {
        var args = new MinimalArgs();
        IRouteableArgs contract = args;
        var named = new RouteableArgument(2, "two", true);

        contract.ForViewModel(1, named);
        contract.ForView("v");
        contract.ForValues(null, 3.5);

        Assert.Equal(new[] { new RouteableArgument(1), named }, args.ViewModelParameters);
        Assert.Equal(new[] { new RouteableArgument("v") }, args.ViewParameters);
        Assert.Equal(new[] { new RouteableArgument(null), new RouteableArgument(3.5) }, args.Values);
    }

    [Fact]
    public void Named_values_are_added_to_the_view_model_the_view_or_the_values()
    {
        var args = new MinimalArgs();
        IRouteableArgs contract = args;

        Assert.Same(args, contract.WithViewModel("id", 7));
        contract.WithView("title", "T", attribute: true);
        contract.WithValue("animal", "cat");

        Assert.Equal(new RouteableArgument(7, "id", false), Assert.Single(args.ViewModelParameters));
        Assert.Equal(new RouteableArgument("T", "title", true), Assert.Single(args.ViewParameters));
        Assert.Equal(new RouteableArgument("cat", "animal", false), Assert.Single(args.Values));
    }

    [Fact]
    public void The_key_of_the_content_is_the_view_parameter_named_key_or_null()
    {
        var args = new MinimalArgs();
        IRouteableArgs contract = args;
        Assert.Null(contract.GetKeyForContent());

        args.ViewParameters.Add(new RouteableArgument("other", "x"));
        args.ViewParameters.Add(new RouteableArgument("k1", Constants.Key));

        Assert.Equal("k1", contract.GetKeyForContent());
    }
}

/// <summary>The array of the generator's models: equal when its content is, whatever the instances.</summary>
public class EquatableArrayTests
{
    [Fact]
    public void A_default_array_is_empty_and_equals_an_empty_one()
    {
        var none = default(EquatableArray<string>);

        Assert.Equal(0, none.Count);
        Assert.Empty(none);
        Assert.True(none.Equals(new EquatableArray<string>([])));
        Assert.Equal(none.GetHashCode(), new EquatableArray<string>([]).GetHashCode());
    }

    [Fact]
    public void Arrays_with_the_same_content_are_equal_and_have_the_same_hash()
    {
        var a = new EquatableArray<string>(["x", "y"]);
        var b = new EquatableArray<string>(["x", "y"]);

        Assert.True(a.Equals(b));
        Assert.True(a.Equals((object)b));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Arrays_that_differ_in_length_or_in_an_item_are_not_equal()
    {
        var a = new EquatableArray<string>(["x", "y"]);

        Assert.False(a.Equals(new EquatableArray<string>(["x"])));
        Assert.False(a.Equals(new EquatableArray<string>(["x", "z"])));
        Assert.False(a.Equals("x"));
        Assert.False(a.Equals(null));
    }

    [Fact]
    public void The_items_are_read_by_index_and_by_enumeration()
    {
        var a = new EquatableArray<string>(["x", "y"]);

        Assert.Equal(2, a.Count);
        Assert.Equal("y", a[1]);
        Assert.Equal(new[] { "x", "y" }, a.ToArray());
        Assert.Throws<IndexOutOfRangeException>(() => default(EquatableArray<string>)[0]);
    }
}

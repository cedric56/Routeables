using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Routing;
using Routeables.Locking;
using Routeables.Routing;

namespace Routeables.Tests.Core;

/// <summary>What a menu view model does by default when the application does not say otherwise.</summary>
public class RouteableMenuViewModelBaseDefaultsTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class Entry(string route) : IRouteableMenuEntry
    {
        public bool IsLock { get; set; }
        public object? ViewModel { get; set; }
        public object? View { get; set; }
        public string Route => route;
        public string Template => route;
        public IRouteableArgs Arguments { get; set; } = null!;
        public string DisplayName => route;
        public string? Path => null;
    }

    private static Router CreateRouter()
    {
        var services = new ServiceCollection();
        services.AddLocking();
        return new Router(new RouterHistory(), new RouterGate(), new RouterLifecycle(), services.BuildServiceProvider());
    }

    [Fact]
    public async Task A_menu_view_model_can_always_be_left_and_does_nothing_when_entered_or_left()
    {
        var sut = TestNavigableViewModel.Create(CreateRouter(), [], initialize: false);
        var args = RouteableArgs.Create();

        Assert.True(await sut.CanLeaveAsync(Ct));
        await sut.OnEnteringAsync(args, Ct);
        await sut.OnLeavingAsync(args, Ct);
    }

    [Fact]
    public async Task Selecting_an_entry_whose_route_does_not_open_answers_no_and_leaves_no_entry_selected()
    {
        var sut = TestNavigableViewModel.Create(CreateRouter(), [], initialize: false);

        var selected = await sut.SelectEntryAsync(new Entry("/not-registered"));

        Assert.False(selected);
    }

    [Fact]
    public void Selecting_nothing_when_nothing_is_selected_leaves_nothing_selected()
    {
        var sut = TestNavigableViewModel.Create(CreateRouter(), [], initialize: false);

        sut.SelectedEntry = null;

        Assert.Null(sut.SelectedEntry);
    }
}

using Avalonia.Controls;
using Routeables.Abstraction;
using Routeables.Avalonia;
using Routeables.Routing;

namespace Routeables.Tests.Avalonia;

public class ViewForTests
{
    private sealed class TestView : RouteableFor<object>
    {
        public IRouteableArgs? InitializedWith { get; private set; }
        public IRouteableArgs? RoutingToWith { get; private set; }
        public IRouteableArgs? RoutingFromWith { get; private set; }
        public bool CanLeave { get; set; } = true;

        public override Task<bool> CanLeaveAsync(CancellationToken ct = default)
            => Task.FromResult(CanLeave);

        public override Task InitializeAsync(IRouteableArgs args, CancellationToken ct = default)
        {
            InitializedWith = args;
            return Task.CompletedTask;
        }

        public override Task OnEnteringAsync(IRouteableArgs context, CancellationToken ct = default)
        {
            RoutingToWith = context;
            return Task.CompletedTask;
        }

        public override Task OnLeavingAsync(IRouteableArgs context, CancellationToken ct = default)
        {
            RoutingFromWith = context;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public void Context_is_the_same_value_as_the_Avalonia_DataContext()
    {
        var view = new TestView();
        var viewModel = new object();

        view.Context = viewModel;

        Assert.Same(viewModel, view.DataContext);
        Assert.Same(viewModel, view.Context);
    }

    [Fact]
    public void DataContext_changes_are_reflected_by_Context()
    {
        var view = new TestView();
        var viewModel = new object();

        view.DataContext = viewModel;

        Assert.Same(viewModel, view.Context);
    }

    [Fact]
    public async Task Routing_lifecycle_overrides_receive_their_context_and_veto_value()
    {
        var view = new TestView { CanLeave = false };
        var args = RouteableArgs.Create().WithRoute("details/42");

        var canRoute = await view.CanLeaveAsync(TestContext.Current.CancellationToken);
        await view.InitializeAsync(args, TestContext.Current.CancellationToken);
        await view.OnEnteringAsync(args, TestContext.Current.CancellationToken);
        await view.OnLeavingAsync(args, TestContext.Current.CancellationToken);

        Assert.False(canRoute);
        Assert.Same(args, view.InitializedWith);
        Assert.Same(args, view.RoutingToWith);
        Assert.Same(args, view.RoutingFromWith);
    }

    [Fact]
    public async Task Default_routing_guard_allows_routing()
    {
        var view = new RouteableFor<object>();

        Assert.True(await view.CanLeaveAsync(TestContext.Current.CancellationToken));
        await view.InitializeAsync(RouteableArgs.Create(), TestContext.Current.CancellationToken);
        await view.OnEnteringAsync(RouteableArgs.Create(), TestContext.Current.CancellationToken);
        await view.OnLeavingAsync(RouteableArgs.Create(), TestContext.Current.CancellationToken);
    }
}

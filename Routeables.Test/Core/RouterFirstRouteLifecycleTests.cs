using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Routing;

namespace Routeables.Tests.Core;

/// <summary>
/// Guards first-routing lifecycle order.
///
/// BUG (docs/AUDIT.md #1): when history is empty, Router.GoTo runs TransitionAsync with a
/// synthetic previous equal to the target, so OnLeaving fires before OnEntering.
/// Desired: first routing only calls OnEntering.
/// </summary>
public class RouterFirstRoutingLifecycleTests
{
    private sealed class TrackingVm : IRouteable
    {
        public int ToCalls { get; private set; }
        public int FromCalls { get; private set; }
        public List<string> Order { get; } = [];

        public Task<bool> CanLeaveAsync(CancellationToken ct = default) => Task.FromResult(true);

        public Task OnEnteringAsync(IRouteableArgs context, CancellationToken ct = default)
        {
            ToCalls++;
            Order.Add("To");
            return Task.CompletedTask;
        }

        public Task OnLeavingAsync(IRouteableArgs context, CancellationToken ct = default)
        {
            FromCalls++;
            Order.Add("From");
            return Task.CompletedTask;
        }
    }

    private sealed class TrackingView : IRouteableFor, IRouteable
    {
        public object? Context { get; set; }
        public int ToCalls { get; private set; }
        public int FromCalls { get; private set; }

        public Task<bool> CanLeaveAsync(CancellationToken ct = default) => Task.FromResult(true);

        public Task OnEnteringAsync(IRouteableArgs context, CancellationToken ct = default)
        {
            ToCalls++;
            return Task.CompletedTask;
        }

        public Task OnLeavingAsync(IRouteableArgs context, CancellationToken ct = default)
        {
            FromCalls++;
            return Task.CompletedTask;
        }
    }

    private static IRouter CreateRouter()
    {
        var history = new RouterHistory();
        var gate = new RouterGate();
        var lifecycle = new RouterLifecycle();
        return new Router(history, gate, lifecycle, new ServiceCollection().BuildServiceProvider());
    }

    [Fact]
    public async Task First_GoTo_should_only_call_OnEntering_not_From()
    {
        var router = CreateRouter();
        var vm = new TrackingVm();
        var view = new TrackingView();

        var result = await router.GoAsync(view, vm, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(1, vm.ToCalls);
        Assert.Equal(0, vm.FromCalls);
        Assert.Equal(new[] { "To" }, vm.Order);
        Assert.Equal(1, view.ToCalls);
        Assert.Equal(0, view.FromCalls);
        Assert.Same(vm, router.CurrentViewModel);
    }

    [Fact]
    public async Task Second_GoTo_calls_From_on_previous_and_To_on_next()
    {
        var router = CreateRouter();
        var vm1 = new TrackingVm();
        var view1 = new TrackingView();
        var vm2 = new TrackingVm();
        var view2 = new TrackingView();

        await router.GoAsync(view1, vm1, cancellationToken: TestContext.Current.CancellationToken);
        // Reset counters after first nav (whatever order it used)
        // We only assert the second transition shape via a fresh pair after a successful first push.
        // If first nav wrongly called From on vm1, FromCalls may already be 1.
        var fromBefore = vm1.FromCalls;

        await router.GoAsync(view2, vm2, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(fromBefore + 1, vm1.FromCalls);
        Assert.Equal(1, vm2.ToCalls);
        Assert.Equal(0, vm2.FromCalls);
        Assert.Same(vm2, router.CurrentViewModel);
        Assert.True(router.CanGoBack);
    }

    [Fact]
    public async Task BackAsync_restores_previous_and_runs_lifecycle()
    {
        var router = CreateRouter();
        var vm1 = new TrackingVm();
        var view1 = new TrackingView();
        var vm2 = new TrackingVm();
        var view2 = new TrackingView();

        await router.GoAsync(view1, vm1, cancellationToken: TestContext.Current.CancellationToken);
        await router.GoAsync(view2, vm2, cancellationToken: TestContext.Current.CancellationToken);

        await router.GoBackAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Same(vm1, router.CurrentViewModel);
        Assert.False(router.CanGoBack);
        Assert.True(router.CanGoForward);
    }
}

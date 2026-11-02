using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Routing;
using Routeables.Test.Abstraction;

namespace Routeables.Tests.Core;

public class RouterTests
{
    private static IRouter CreateRouter()
    {
        var history = new RouterHistory();
        var gate = new RouterGate();
        var lifecycle = new RouterLifecycle();
        return new Router(history, gate, lifecycle, new ServiceCollection().BuildServiceProvider());
    }

    [Fact]
    public async Task GoTo_AddsEntryAndMakesItCurrent()
    {
        var router = CreateRouter();
        var viewModel = new TestViewModel();
        var view = new TestView();

        var result = await router.GoAsync(view, viewModel, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Same(view, view);
        Assert.Same(viewModel, view.Context);
        Assert.Same(viewModel, router.CurrentViewModel);
        Assert.Same(view, router.CurrentView);
        Assert.False(router.CanGoBack);
        Assert.False(router.CanGoForward);
    }

    [Fact]
    public async Task GoTo_ConsultsCurrentViewAndViewModelBeforeLeaving()
    {
        var router = CreateRouter();
        var firstViewModel = new TestViewModel();
        var firstView = new TestNavigableView();
        await router.GoAsync(firstView, firstViewModel, cancellationToken: TestContext.Current.CancellationToken);

        firstViewModel.Events.Clear();
        firstView.Events.Clear();

        var secondViewModel = new TestViewModel();
        var secondView = new TestNavigableView();
        await router.GoAsync(secondView, secondViewModel, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(["CanLeave", "From"], firstViewModel.Events);
        Assert.Equal(["CanLeave", "From"], firstView.Events);
        Assert.Equal(["To"], secondViewModel.Events);
        Assert.Equal(["To"], secondView.Events);
    }

    [Fact]
    public async Task GoTo_DoesNotRouteWhenCurrentViewRejects()
    {
        var router = CreateRouter();
        var firstViewModel = new TestViewModel();
        var firstView = new TestNavigableView { CanLeave = false };
        await router.GoAsync(firstView, firstViewModel, cancellationToken: TestContext.Current.CancellationToken);

        var secondViewModel = new TestViewModel();
        var secondView = new TestView();
        var result = await router.GoAsync(secondView, secondViewModel, cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Same(firstViewModel, router.CurrentViewModel);
        Assert.Same(firstView, router.CurrentView);
        Assert.Null(secondView.Context);
    }

    [Fact]
    public async Task GoTo_ExceptionInOnLeaving_KeepsRouterState()
    {
        var router = CreateRouter();
        var firstViewModel = new ThrowingFromViewModel();
        var firstView = new TestView();
        await router.GoAsync(firstView, firstViewModel, cancellationToken: TestContext.Current.CancellationToken);

        var secondViewModel = new TestViewModel();
        var secondView = new TestView();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => router.GoAsync(secondView, secondViewModel, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Same(firstViewModel, router.CurrentViewModel);
        Assert.Same(firstView, router.CurrentView);
        Assert.Null(secondView.Context);
    }

    [Fact]
    public async Task BackAsync_ReturnsCancelled_WhenHistoryEmpty()
    {
        var router = CreateRouter();
        var args = await router.GoBackAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(args.Cancelled);
    }

    [Fact]
    public async Task BackAsync_And_ForwardAsync_TraverseHistory()
    {
        var router = CreateRouter();
        var vm1 = new TestViewModel();
        var v1 = new TestView();
        var vm2 = new TestViewModel();
        var v2 = new TestView();
        var vm3 = new TestViewModel();
        var v3 = new TestView();

        await router.GoAsync(v1, vm1, cancellationToken: TestContext.Current.CancellationToken);
        await router.GoAsync(v2, vm2, cancellationToken: TestContext.Current.CancellationToken);
        await router.GoAsync(v3, vm3, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(router.CanGoBack);
        Assert.False(router.CanGoForward);

        await router.GoBackAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Same(vm2, router.CurrentViewModel);
        Assert.True(router.CanGoForward);

        await router.GoBackAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Same(vm1, router.CurrentViewModel);
        Assert.False(router.CanGoBack);

        await router.ForwardAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Same(vm2, router.CurrentViewModel);
        await router.ForwardAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Same(vm3, router.CurrentViewModel);
        Assert.False(router.CanGoForward);
    }

    [Fact]
    public async Task GoTo_AfterBack_DiscardsForwardStack()
    {
        var router = CreateRouter();
        await router.GoAsync(new TestView(), new TestViewModel(), cancellationToken: TestContext.Current.CancellationToken);
        await router.GoAsync(new TestView(), new TestViewModel(), cancellationToken: TestContext.Current.CancellationToken);
        await router.GoAsync(new TestView(), new TestViewModel(), cancellationToken: TestContext.Current.CancellationToken);
        await router.GoBackAsync(cancellationToken: TestContext.Current.CancellationToken);

        var vmNew = new TestViewModel();
        var vNew = new TestView();
        await router.GoAsync(vNew, vmNew, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Same(vmNew, router.CurrentViewModel);
        Assert.False(router.CanGoForward);
        Assert.True(router.CanGoBack);
    }

    [Fact]
    public async Task EraseHistoryAsync_ClearsRoutingState()
    {
        var router = CreateRouter();
        await router.GoAsync(new TestView(), new TestViewModel(), cancellationToken: TestContext.Current.CancellationToken);
        await router.GoAsync(new TestView(), new TestViewModel(), cancellationToken: TestContext.Current.CancellationToken);

        await router.EraseHistoryAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(router.CurrentViewModel);
        Assert.Null(router.CurrentView);
        Assert.False(router.CanGoBack);
        Assert.False(router.CanGoForward);
    }

    [Fact]
    public async Task GoTo_ThrowsOnNullViewOrViewModel()
    {
        var router = CreateRouter();
        await Assert.ThrowsAsync<ArgumentNullException>(() => router.GoAsync(null!, new TestViewModel(), cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentNullException>(() => router.GoAsync(new TestView(), null!, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Reentrant_GoTo_Throws()
    {
        var router = CreateRouter();
        var reentrant = new ReentrantViewModel(router);

        // The message matters: before the gate was fixed this test passed for the wrong reason, because the
        // nested call failed with "No such view registered" instead of being rejected as reentrant.
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => router.GoAsync(new TestView(), reentrant, cancellationToken: TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(5), cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("Reentrant", ex.Message);
    }

    private sealed class ThrowingFromViewModel : IRouteable
    {
        public Task<bool> CanLeaveAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task OnEnteringAsync(IRouteableArgs context, CancellationToken ct = default) => Task.CompletedTask;
        public Task OnLeavingAsync(IRouteableArgs context, CancellationToken ct = default) =>
            throw new InvalidOperationException("from failed");
    }

    private sealed class ReentrantViewModel(IRouter router) : IRouteable
    {
        public Task<bool> CanLeaveAsync(CancellationToken ct = default) => Task.FromResult(true);

        public async Task OnEnteringAsync(IRouteableArgs context, CancellationToken ct = default)
        {
            // Attempt nested routing while gate is held
            await router.GoAsync(new TestView(), new TestViewModel(), cancellationToken: TestContext.Current.CancellationToken);
        }

        public Task OnLeavingAsync(IRouteableArgs context, CancellationToken ct = default) => Task.CompletedTask;
    }
}

using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Routing;
using Routeables.Test.Abstraction;

namespace Routeables.Tests.Core;

/// <summary>
/// What the router looks like after a routing fails or is cancelled halfway, and under concurrent calls:
/// the state stays the one of the page that is displayed, and the gate is released so the next routing works.
/// </summary>
public class RouterFailureTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static IRouter CreateRouter()
        => new Router(new RouterHistory(), new RouterGate(), new RouterLifecycle(), new ServiceCollection().BuildServiceProvider());

    private sealed class FailingTarget(bool inInitialize) : IRouteable, IInitializeAsync
    {
        public Task<bool> CanLeaveAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task OnLeavingAsync(IRouteableArgs context, CancellationToken ct = default) => Task.CompletedTask;

        public Task OnEnteringAsync(IRouteableArgs context, CancellationToken ct = default)
            => inInitialize ? Task.CompletedTask : throw new InvalidOperationException("to failed");

        public Task InitializeAsync(IRouteableArgs args, CancellationToken ct = default)
            => inInitialize ? throw new InvalidOperationException("initialize failed") : Task.CompletedTask;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_target_that_fails_while_arriving_leaves_the_router_usable(bool inInitialize)
    {
        var router = CreateRouter();
        var first = new TestViewModel();
        await router.GoAsync(new TestView(), first, cancellationToken: Ct);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => router.GoAsync(new TestView(), new FailingTarget(inInitialize), cancellationToken: Ct));

        // The gate was released: another routing completes instead of waiting forever.
        var next = new TestViewModel();
        var result = await router.GoAsync(new TestView(), next, cancellationToken: Ct).WaitAsync(TimeSpan.FromSeconds(5), Ct);

        Assert.True(result.Success);
        Assert.Same(next, router.CurrentViewModel);
    }

    [Fact]
    public async Task A_routing_cancelled_before_it_starts_does_not_change_the_current_page()
    {
        var router = CreateRouter();
        var first = new TestViewModel();
        await router.GoAsync(new TestView(), first, cancellationToken: Ct);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        try { await router.GoAsync(new TestView(), new TestViewModel(), cancellationToken: cts.Token); }
        catch (OperationCanceledException) { }

        Assert.Same(first, router.CurrentViewModel);
        var result = await router.GoAsync(new TestView(), new TestViewModel(), cancellationToken: Ct).WaitAsync(TimeSpan.FromSeconds(5), Ct);
        Assert.True(result.Success);
    }

    [Fact]
    public async Task RouteChanges_started_together_run_one_after_the_other()
    {
        var router = CreateRouter();
        var viewModels = Enumerable.Range(0, 50).Select(_ => new TestViewModel()).ToArray();

        var results = await Task.WhenAll(viewModels
            .Select(vm => Task.Run(() => router.GoAsync(new TestView(), vm, cancellationToken: Ct), Ct)))
            .WaitAsync(TimeSpan.FromSeconds(20), Ct);

        Assert.True(results.All(r => r.Success));
        Assert.Equal(50, router.History.Count);
        Assert.Contains(router.CurrentViewModel, viewModels);

        // Each page left exactly once, in a sequence: 49 of them were left for the next one.
        Assert.Equal(49, viewModels.Count(vm => vm.Events.Contains("From")));
    }
}

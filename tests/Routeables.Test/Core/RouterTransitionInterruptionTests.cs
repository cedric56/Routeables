using Routeables.Abstraction.Lifecycle;
using Routeables.Abstraction.Routing;
using Routeables.Routing;
using Routeables.Test.Abstraction;

namespace Routeables.Tests.Core;

/// <summary>A route change that ends before its transition does (a hook that fails, a token cancelled, a newer route change): what stays in the history, and whether the router still works.</summary>
public class RouterTransitionInterruptionTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Router CreateRouter(RouterConcurrency concurrency = RouterConcurrency.Queue)
        => new(new RouterHistory(), new RouterGate(concurrency), new RouterLifecycle(), new ServiceCollection().BuildServiceProvider());

    /// <summary>A page whose hooks can be made to fail or to wait for the token they receive.</summary>
    private sealed class Page : IRouteable
    {
        public bool FailOnEntering { get; set; }
        public bool FailOnLeaving { get; set; }
        public bool WaitOnEntering { get; set; }
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<bool> CanLeaveAsync(CancellationToken ct = default) => Task.FromResult(true);

        public Task OnLeavingAsync(IRouteableArgs context, CancellationToken ct = default)
            => FailOnLeaving ? throw new InvalidOperationException("leaving failed") : Task.CompletedTask;

        public async Task OnEnteringAsync(IRouteableArgs context, CancellationToken ct = default)
        {
            if (FailOnEntering)
                throw new InvalidOperationException("entering failed");
            if (WaitOnEntering)
            {
                Started.TrySetResult();
                await Task.Delay(Timeout.Infinite, ct);
            }
        }
    }

    [Fact]
    public async Task A_page_whose_arrival_fails_does_not_enter_the_history_and_the_page_left_stays()
    {
        var router = CreateRouter();
        var home = new Page();
        await router.GoAsync(new TestView(), home, cancellationToken: Ct);
        var outcomes = new List<RouteableOutcome>();
        router.Routed += (_, e) => outcomes.Add(e.Outcome);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => router.GoAsync(new TestView(), new Page { FailOnEntering = true }, cancellationToken: Ct));

        Assert.Equal(1, router.History.Count);
        Assert.Same(home, router.CurrentViewModel);
        Assert.Contains(RouteableOutcome.Failed, outcomes);
    }

    [Fact]
    public async Task A_page_whose_leaving_fails_stays_and_nothing_enters_the_history()
    {
        var router = CreateRouter();
        var home = new Page { FailOnLeaving = true };
        await router.GoAsync(new TestView(), home, cancellationToken: Ct);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => router.GoAsync(new TestView(), new Page(), cancellationToken: Ct));

        Assert.Equal(1, router.History.Count);
        Assert.Same(home, router.CurrentViewModel);
    }

    [Fact]
    public async Task The_router_goes_on_working_after_a_route_change_failed()
    {
        var router = CreateRouter();
        await router.GoAsync(new TestView(), new Page(), cancellationToken: Ct);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => router.GoAsync(new TestView(), new Page { FailOnEntering = true }, cancellationToken: Ct));

        var next = new Page();
        var result = await router.GoAsync(new TestView(), next, cancellationToken: Ct).WaitAsync(TimeSpan.FromSeconds(5), Ct);

        Assert.True(result.Success);
        Assert.Same(next, router.CurrentViewModel);
        Assert.Equal(2, router.History.Count);
    }

    [Fact]
    public async Task A_route_change_whose_token_is_cancelled_during_the_arrival_changes_nothing()
    {
        var router = CreateRouter();
        var home = new Page();
        await router.GoAsync(new TestView(), home, cancellationToken: Ct);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var slow = new Page { WaitOnEntering = true };

        var change = router.GoAsync(new TestView(), slow, cancellationToken: cts.Token);
        await slow.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), Ct);
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => change.WaitAsync(TimeSpan.FromSeconds(5), Ct));
        Assert.Equal(1, router.History.Count);
        Assert.Same(home, router.CurrentViewModel);
        Assert.True((await router.GoAsync(new TestView(), new Page(), cancellationToken: Ct).WaitAsync(TimeSpan.FromSeconds(5), Ct)).Success); // the gate is free
    }

    [Fact]
    public async Task A_back_replaced_by_a_newer_route_change_ends_on_the_newer_page()
    {
        var router = CreateRouter(RouterConcurrency.CancelCurrent);
        var first = new Page();
        await router.GoAsync(new TestView(), first, cancellationToken: Ct);
        await router.GoAsync(new TestView(), new Page(), cancellationToken: Ct);
        first.WaitOnEntering = true; // going back to it now waits

        var back = router.GoBackAsync(Ct);
        await first.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), Ct);
        var newest = new Page();
        var result = await router.GoAsync(new TestView(), newest, cancellationToken: Ct).WaitAsync(TimeSpan.FromSeconds(5), Ct);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => back.WaitAsync(TimeSpan.FromSeconds(5), Ct));
        Assert.True(result.Success);
        Assert.Same(newest, router.CurrentViewModel);
        Assert.Equal(router.History.Count - 1, router.History.CurrentIndex);
    }
}

using Routeables.Abstraction.Lifecycle;
using Routeables.Abstraction.Routing;
using Routeables.Routing;
using Routeables.Test.Abstraction;

namespace Routeables.Tests.Core;

/// <summary>
/// <see cref="RouterConcurrency.CancelCurrent"/>: a route change that comes while another is in progress cancels it, then takes its place.
/// </summary>
public class RouterCancelCurrentTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Router CreateRouter(RouterConcurrency concurrency)
        => new(new RouterHistory(), new RouterGate(concurrency), new RouterLifecycle(), new ServiceCollection().BuildServiceProvider());

    /// <summary>A page whose arrival lasts until the token it receives is cancelled.</summary>
    private sealed class SlowTarget : IRouteable
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Cancelled { get; private set; }

        public Task<bool> CanLeaveAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task OnLeavingAsync(IRouteableArgs context, CancellationToken ct = default) => Task.CompletedTask;

        public async Task OnEnteringAsync(IRouteableArgs context, CancellationToken ct = default)
        {
            Started.TrySetResult();
            try { await Task.Delay(Timeout.Infinite, ct); }
            catch (OperationCanceledException) { Cancelled = true; throw; }
        }
    }

    [Fact]
    public async Task A_new_route_change_cancels_the_one_in_progress_and_takes_its_place()
    {
        var router = CreateRouter(RouterConcurrency.CancelCurrent);
        var first = new TestViewModel();
        await router.GoAsync(new TestView(), first, cancellationToken: Ct);

        var slow = new SlowTarget();
        var slowChange = router.GoAsync(new TestView(), slow, cancellationToken: Ct);
        await slow.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), Ct);

        var last = new TestViewModel();
        var result = await router.GoAsync(new TestView(), last, cancellationToken: Ct).WaitAsync(TimeSpan.FromSeconds(5), Ct);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => slowChange.WaitAsync(TimeSpan.FromSeconds(5), Ct));
        Assert.True(slow.Cancelled);
        Assert.True(result.Success);
        Assert.Same(last, router.CurrentViewModel);
    }

    [Fact]
    public async Task A_cancelled_route_change_is_reported_as_cancelled()
    {
        var router = CreateRouter(RouterConcurrency.CancelCurrent);
        var outcomes = new List<RouteableOutcome>();
        router.Routed += (_, e) => outcomes.Add(e.Outcome);

        var slow = new SlowTarget();
        var slowChange = router.GoAsync(new TestView(), slow, cancellationToken: Ct);
        await slow.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), Ct);
        await router.GoAsync(new TestView(), new TestViewModel(), cancellationToken: Ct).WaitAsync(TimeSpan.FromSeconds(5), Ct);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => slowChange);

        Assert.Contains(RouteableOutcome.Cancelled, outcomes);
    }

    [Fact]
    public async Task Of_several_route_changes_waiting_only_the_last_one_runs()
    {
        var router = CreateRouter(RouterConcurrency.CancelCurrent);
        var slow = new SlowTarget();
        var slowChange = router.GoAsync(new TestView(), slow, cancellationToken: Ct);
        await slow.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), Ct);

        // Each one starts while the previous one has not finished: they all try to replace the slow one.
        var viewModels = Enumerable.Range(0, 10).Select(_ => new TestViewModel()).ToArray();
        var changes = viewModels.Select(vm => router.GoAsync(new TestView(), vm, cancellationToken: Ct)).ToArray();

        var last = await changes[^1].WaitAsync(TimeSpan.FromSeconds(5), Ct);
        Assert.True(last.Success);
        Assert.Same(viewModels[^1], router.CurrentViewModel);
        Assert.Equal(1, router.History.Count);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => slowChange);
    }

    [Fact]
    public async Task The_gate_is_free_after_a_route_change_was_cancelled()
    {
        var router = CreateRouter(RouterConcurrency.CancelCurrent);
        var slow = new SlowTarget();
        var slowChange = router.GoAsync(new TestView(), slow, cancellationToken: Ct);
        await slow.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), Ct);
        await router.GoAsync(new TestView(), new TestViewModel(), cancellationToken: Ct).WaitAsync(TimeSpan.FromSeconds(5), Ct);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => slowChange);

        var result = await router.GoAsync(new TestView(), new TestViewModel(), cancellationToken: Ct).WaitAsync(TimeSpan.FromSeconds(5), Ct);
        Assert.True(result.Success);
    }

    [Fact]
    public async Task Queue_lets_the_route_change_in_progress_finish()
    {
        var router = CreateRouter(RouterConcurrency.Queue);
        var first = new TestViewModel();
        var second = new TestViewModel();

        var results = await Task.WhenAll(
            router.GoAsync(new TestView(), first, cancellationToken: Ct),
            router.GoAsync(new TestView(), second, cancellationToken: Ct)).WaitAsync(TimeSpan.FromSeconds(5), Ct);

        Assert.All(results, r => Assert.True(r.Success));
        Assert.Equal(2, router.History.Count);
    }

    [Fact]
    public void SetConcurrency_is_read_per_region()
    {
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.AddRegion("side");
        services.SetConcurrency(RouterConcurrency.CancelCurrent, "side");

        var policies = services.BuildServiceProvider().GetRequiredService<ConcurrencyPolicies>();
        Assert.Equal(RouterConcurrency.Queue, policies.For(null));
        Assert.Equal(RouterConcurrency.CancelCurrent, policies.For("side"));
    }
}

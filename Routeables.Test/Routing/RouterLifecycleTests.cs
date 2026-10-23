using Routeables.Abstraction;
using Routeables.Routing;

namespace Routeables.Tests.Routing;

public class RouterLifecycleTests
{
    [Fact]
    public async Task CanLeave_checks_view_model_then_view_and_stops_when_a_guard_declines()
    {
        var calls = new List<string>();
        var viewModel = new LifecycleProbe("view-model", calls) { CanLeave = false };
        var view = new LifecycleProbe("view", calls);
        var lifecycle = new RouterLifecycle();

        var canLeave = await lifecycle.CanLeaveAsync(Entry(viewModel, view), ct: TestContext.Current.CancellationToken);

        Assert.False(canLeave);
        Assert.Equal(["view-model:guard"], calls);
    }

    [Fact]
    public async Task Transition_runs_leave_before_enter_in_view_model_then_view_order()
    {
        var calls = new List<string>();
        var leaving = Entry(new LifecycleProbe("leaving-vm", calls), new LifecycleProbe("leaving-view", calls));
        var entering = Entry(new LifecycleProbe("entering-vm", calls), new LifecycleProbe("entering-view", calls));
        var lifecycle = new RouterLifecycle();

        await lifecycle.TransitionAsync(leaving, entering, ct: TestContext.Current.CancellationToken);

        Assert.Equal(
        [
            "leaving-vm:from", "leaving-view:from",
            "entering-vm:to", "entering-view:to",
        ], calls);
    }

    [Fact]
    public async Task Transition_restores_leaving_entry_and_preserves_original_exception_if_recovery_fails()
    {
        var calls = new List<string>();
        var routingFailure = new InvalidOperationException("target failed");
        var recoveryFailure = new InvalidOperationException("recovery failed");
        var leavingViewModel = new LifecycleProbe("leaving-vm", calls)
        {
            OnTo = (_, _) => throw recoveryFailure,
        };
        var leaving = Entry(leavingViewModel, new LifecycleProbe("leaving-view", calls));
        var entering = Entry(new LifecycleProbe("entering-vm", calls)
        {
            OnTo = (_, _) => throw routingFailure,
        }, new LifecycleProbe("entering-view", calls));
        var lifecycle = new RouterLifecycle();

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => lifecycle.TransitionAsync(leaving, entering, ct: TestContext.Current.CancellationToken));

        Assert.Same(routingFailure, thrown);
        Assert.Equal(
        [
            "leaving-vm:from", "leaving-view:from",
            "entering-vm:to",
            "leaving-vm:to",
        ], calls);
    }

    private sealed class Sink : IRouteableErrorSink
    {
        public List<(string Source, Exception Exception)> Reported { get; } = [];
        public void Report(string source, Exception exception) => Reported.Add((source, exception));
    }

    [Fact]
    public async Task A_recovery_that_fails_is_reported_while_the_original_exception_is_thrown()
    {
        var calls = new List<string>();
        var routingFailure = new InvalidOperationException("target failed");
        var recoveryFailure = new InvalidOperationException("recovery failed");
        var leaving = Entry(new LifecycleProbe("leaving-vm", calls) { OnTo = (_, _) => throw recoveryFailure }, new LifecycleProbe("leaving-view", calls));
        var entering = Entry(new LifecycleProbe("entering-vm", calls) { OnTo = (_, _) => throw routingFailure }, new LifecycleProbe("entering-view", calls));
        var sink = new Sink();
        var provider = new Microsoft.Extensions.DependencyInjection.ServiceCollection()
            .AddSingleton<IRouteableErrorSink>(sink).BuildServiceProvider();
        var lifecycle = new RouterLifecycle(provider);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => lifecycle.TransitionAsync(leaving, entering, ct: TestContext.Current.CancellationToken));

        Assert.Same(routingFailure, thrown);
        var reported = Assert.Single(sink.Reported);
        Assert.Equal("RouterLifecycle.Recover", reported.Source);
        Assert.Same(recoveryFailure, reported.Exception);
    }

    [Fact]
    public async Task Initialize_calls_a_shared_view_and_view_model_only_once()
    {
        var calls = new List<string>();
        var shared = new LifecycleProbe("shared", calls);
        var entry = Entry(shared, shared);
        var lifecycle = new RouterLifecycle();

        await lifecycle.InitializeAsync(entry, ct: TestContext.Current.CancellationToken);

        Assert.Equal(["shared:init"], calls);
    }

    private static RouterEntry Entry(object viewModel, object view) =>
        new(viewModel, view, RouteableArgs.Create());

    private sealed class LifecycleProbe(string name, List<string> calls) : IRouteable, IInitializeAsync
    {
        public bool CanLeave { get; init; } = true;
        public Func<IRouteableArgs, CancellationToken, Task>? OnTo { get; init; }
        public int DestroyCount { get; private set; }
        public CancellationToken DestroyToken { get; private set; }

        public Task<bool> CanLeaveAsync(CancellationToken ct = default)
        {
            calls.Add($"{name}:guard");
            return Task.FromResult(CanLeave);
        }

        public Task OnEnteringAsync(IRouteableArgs context, CancellationToken ct = default)
        {
            calls.Add($"{name}:to");
            return OnTo?.Invoke(context, ct) ?? Task.CompletedTask;
        }

        public Task OnLeavingAsync(IRouteableArgs context, CancellationToken ct = default)
        {
            calls.Add($"{name}:from");
            return Task.CompletedTask;
        }

        public Task InitializeAsync(IRouteableArgs args, CancellationToken ct = default)
        {
            calls.Add($"{name}:init");
            return Task.CompletedTask;
        }

        public Task DestroyAsync(CancellationToken ct = default)
        {
            DestroyCount++;
            DestroyToken = ct;
            return Task.CompletedTask;
        }
    }
}

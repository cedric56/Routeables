using Routeables.Abstraction.Errors;
using Routeables.Abstraction.Lifecycle;
using Routeables.Abstraction.Routing;
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

    [Fact]
    public async Task A_view_that_fails_to_enter_tells_the_view_model_that_was_entered_it_is_left()
    {
        var calls = new List<string>();
        var failure = new InvalidOperationException("the view failed to enter");
        var leaving = Entry(new LifecycleProbe("leaving-vm", calls), new LifecycleProbe("leaving-view", calls));
        var entering = Entry(new LifecycleProbe("entering-vm", calls), new LifecycleProbe("entering-view", calls) { OnTo = (_, _) => throw failure });

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => new RouterLifecycle().TransitionAsync(leaving, entering, ct: TestContext.Current.CancellationToken));

        Assert.Same(failure, thrown);
        // What the view model started in OnEntering is undone (OnLeaving), then the page that stays is entered again.
        Assert.Equal(
        [
            "leaving-vm:from", "leaving-view:from",
            "entering-vm:to", "entering-view:to",
            "entering-vm:from",
            "leaving-vm:to", "leaving-view:to",
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
        Assert.StartsWith("RouterLifecycle.", reported.Source);
        Assert.Same(recoveryFailure, reported.Exception);
    }

    /// <summary>
    /// The contract of a failed OnLeavingAsync of the view: the route change fails with that exception, the entering page is never touched,
    /// and the page being left, which stays the current one and whose view model was already told OnLeaving, is entered again.
    /// </summary>
    [Fact]
    public async Task A_leaving_callback_of_the_view_that_fails_stops_the_transition_and_enters_the_page_left_again()
    {
        var calls = new List<string>();
        var failure = new InvalidOperationException("the view failed to leave");
        var leaving = Entry(new LifecycleProbe("leaving-vm", calls), new LifecycleProbe("leaving-view", calls) { OnFrom = (_, _) => throw failure });
        var entering = Entry(new LifecycleProbe("entering-vm", calls), new LifecycleProbe("entering-view", calls));
        var lifecycle = new RouterLifecycle();

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => lifecycle.TransitionAsync(leaving, entering, ct: TestContext.Current.CancellationToken));

        Assert.Same(failure, thrown);
        // The view never left (its OnLeaving failed): only the view model is entered again.
        Assert.Equal(["leaving-vm:from", "leaving-view:from", "leaving-vm:to"], calls);
    }

    [Fact]
    public async Task A_leaving_callback_of_the_view_model_that_fails_leaves_nothing_to_enter_again()
    {
        var calls = new List<string>();
        var failure = new InvalidOperationException("the view model failed to leave");
        var leaving = Entry(new LifecycleProbe("leaving-vm", calls) { OnFrom = (_, _) => throw failure }, new LifecycleProbe("leaving-view", calls));
        var entering = Entry(new LifecycleProbe("entering-vm", calls), new LifecycleProbe("entering-view", calls));

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => new RouterLifecycle().TransitionAsync(leaving, entering, ct: TestContext.Current.CancellationToken));

        Assert.Same(failure, thrown);
        Assert.Equal(["leaving-vm:from"], calls);
    }

    [Fact]
    public async Task A_view_model_that_cannot_be_entered_again_after_the_view_failed_to_leave_is_reported()
    {
        var calls = new List<string>();
        var failure = new InvalidOperationException("the view failed to leave");
        var recoveryFailure = new InvalidOperationException("the view model failed to enter again");
        var leaving = Entry(new LifecycleProbe("leaving-vm", calls) { OnTo = (_, _) => throw recoveryFailure }, new LifecycleProbe("leaving-view", calls) { OnFrom = (_, _) => throw failure });
        var entering = Entry(new LifecycleProbe("entering-vm", calls), new LifecycleProbe("entering-view", calls));
        var sink = new Sink();
        var provider = new Microsoft.Extensions.DependencyInjection.ServiceCollection()
            .AddSingleton<IRouteableErrorSink>(sink).BuildServiceProvider();

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => new RouterLifecycle(provider).TransitionAsync(leaving, entering, ct: TestContext.Current.CancellationToken));

        Assert.Same(failure, thrown);
        Assert.Same(recoveryFailure, Assert.Single(sink.Reported).Exception);
    }

    [Fact]
    public async Task A_view_model_that_fails_to_be_left_after_the_view_failed_to_enter_is_reported()
    {
        var calls = new List<string>();
        var failure = new InvalidOperationException("the view failed to enter");
        var undoFailure = new InvalidOperationException("the view model failed to leave");
        var leaving = Entry(new LifecycleProbe("leaving-vm", calls), new LifecycleProbe("leaving-view", calls));
        var entering = Entry(new LifecycleProbe("entering-vm", calls) { OnFrom = (_, _) => throw undoFailure }, new LifecycleProbe("entering-view", calls) { OnTo = (_, _) => throw failure });
        var sink = new Sink();
        var provider = new Microsoft.Extensions.DependencyInjection.ServiceCollection()
            .AddSingleton<IRouteableErrorSink>(sink).BuildServiceProvider();

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => new RouterLifecycle(provider).TransitionAsync(leaving, entering, ct: TestContext.Current.CancellationToken));

        Assert.Same(failure, thrown);
        Assert.Same(undoFailure, Assert.Single(sink.Reported).Exception);
        // The page that stays is still entered again.
        Assert.Equal("leaving-view:to", calls[^1]);
    }

    [Fact]
    public async Task A_revert_that_fails_is_reported_and_not_thrown()
    {
        var calls = new List<string>();
        var failure = new InvalidOperationException("the entered page failed to leave");
        var previous = Entry(new LifecycleProbe("previous-vm", calls), new LifecycleProbe("previous-view", calls));
        var entered = Entry(new LifecycleProbe("entered-vm", calls) { OnFrom = (_, _) => throw failure }, new LifecycleProbe("entered-view", calls));
        var sink = new Sink();
        var provider = new Microsoft.Extensions.DependencyInjection.ServiceCollection()
            .AddSingleton<IRouteableErrorSink>(sink).BuildServiceProvider();

        await new RouterLifecycle(provider).RevertAsync(previous, entered);

        Assert.Same(failure, Assert.Single(sink.Reported).Exception);
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
        public Func<IRouteableArgs, CancellationToken, Task>? OnFrom { get; init; }
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
            return OnFrom?.Invoke(context, ct) ?? Task.CompletedTask;
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

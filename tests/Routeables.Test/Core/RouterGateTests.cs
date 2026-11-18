using Routeables.Routing;

namespace Routeables.Tests.Core;

/// <summary>Tests the real <see cref="RouterGate"/> (not a copy of it).</summary>
public class RouterGateTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task A_newer_caller_that_comes_before_the_operation_is_published_still_cancels_it()
    {
        // The gate is held, the operation is not yet the one in progress: a newer route change finds nothing to cancel, and asked for it to be replaced.
        var gate = new RouterGate(RouterConcurrency.CancelCurrent);
        Task? second = null;
        gate.BeforeBegin = () =>
        {
            gate.BeforeBegin = null;
            Task.Run(() => { second = gate.EnterAsync(cancellationToken: TestContext.Current.CancellationToken); }, TestContext.Current.CancellationToken).Wait(TestContext.Current.CancellationToken);
        };

        await gate.EnterAsync(cancellationToken: TestContext.Current.CancellationToken);
        try { Assert.True(gate.Observe(TestContext.Current.CancellationToken).IsCancellationRequested); }
        finally { gate.Exit(); }

        Assert.NotNull(second);
        await second!.WaitAsync(Timeout, cancellationToken: TestContext.Current.CancellationToken);
        gate.Exit();
    }

    [Fact]
    public async Task A_caller_cancelled_by_its_own_token_does_not_replace_the_ones_before_it()
    {
        // A runs, B waits, C comes and is cancelled: the last one still wanted is B, which must run.
        var gate = new RouterGate(RouterConcurrency.CancelCurrent);
        var aEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var aMayEnd = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<string> Run(CancellationToken caller, Func<Task>? inside = null)
        {
            try { await gate.EnterAsync(cancellationToken: caller); }
            catch (OperationCanceledException) { gate.Abort(); return "cancelled"; }
            try
            {
                var token = gate.Observe(caller);
                if (inside is not null) await inside();
                token.ThrowIfCancellationRequested();
                return "ran";
            }
            catch (OperationCanceledException) { return "cancelled"; }
            finally { gate.Exit(); }
        }

        var a = Task.Run(() => Run(default, async () => { aEntered.SetResult(); await aMayEnd.Task; }), TestContext.Current.CancellationToken);
        await aEntered.Task;
        var b = Task.Run(() => Run(default), TestContext.Current.CancellationToken);
        await Task.Delay(50, TestContext.Current.CancellationToken);
        using var own = new CancellationTokenSource();
        var c = Task.Run(() => Run(own.Token), TestContext.Current.CancellationToken);
        await Task.Delay(50, TestContext.Current.CancellationToken);
        own.Cancel();
        await Task.Delay(50, TestContext.Current.CancellationToken);
        aMayEnd.SetResult();

        Assert.Equal(["cancelled", "ran", "cancelled"], await Task.WhenAll(a, b, c).WaitAsync(Timeout, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_caller_that_is_already_cancelled_replaces_nobody()
    {
        var gate = new RouterGate(RouterConcurrency.CancelCurrent);
        var entered = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var mayEnd = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var running = Task.Run(async () =>
        {
            await gate.EnterAsync(cancellationToken: TestContext.Current.CancellationToken);
            try { entered.SetResult(gate.Observe(TestContext.Current.CancellationToken)); await mayEnd.Task; }
            finally { gate.Exit(); }
        }, TestContext.Current.CancellationToken);
        var token = await entered.Task;

        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        Assert.Throws<OperationCanceledException>(() => { _ = gate.EnterAsync(cancellationToken: cancelled.Token); });

        Assert.False(token.IsCancellationRequested);
        mayEnd.SetResult();
        await running.WaitAsync(Timeout, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Entering_twice_in_the_same_flow_throws_instead_of_waiting_forever()
    {
        var gate = new RouterGate();
        await gate.EnterAsync(cancellationToken: TestContext.Current.CancellationToken);

        try
        {
            Assert.Throws<InvalidOperationException>(() => { _ = gate.EnterAsync(cancellationToken: TestContext.Current.CancellationToken); });
        }
        finally
        {
            gate.Exit();
        }
    }

    [Fact]
    public async Task The_flag_is_visible_to_callbacks_awaited_after_entering()
    {
        // Regression: EnterAsync was an async method, so the AsyncLocal it set was lost when it returned
        // and a nested routing waited for the gate its own flow was holding.
        var gate = new RouterGate();
        var rejected = false;

        async Task Callback()
        {
            await Task.Yield();
            try { await gate.EnterAsync(cancellationToken: TestContext.Current.CancellationToken); }
            catch (InvalidOperationException) { rejected = true; }
        }

        await gate.EnterAsync(cancellationToken: TestContext.Current.CancellationToken);
        try { await Callback().WaitAsync(Timeout, cancellationToken: TestContext.Current.CancellationToken); }
        finally { gate.Exit(); }

        Assert.True(rejected);
    }

    [Fact]
    public async Task Exit_allows_the_gate_to_be_entered_again()
    {
        var gate = new RouterGate();

        await gate.EnterAsync(cancellationToken: TestContext.Current.CancellationToken);
        gate.Exit();

        await gate.EnterAsync(cancellationToken: TestContext.Current.CancellationToken).WaitAsync(Timeout, cancellationToken: TestContext.Current.CancellationToken);
        gate.Exit();
    }

    [Fact]
    public async Task Another_flow_waits_for_the_gate()
    {
        var gate = new RouterGate();
        await gate.EnterAsync(cancellationToken: TestContext.Current.CancellationToken);

        Task other;
        using (ExecutionContext.SuppressFlow())   // an unrelated caller does not inherit our flag
        {
            other = Task.Run(async () =>
            {
                await gate.EnterAsync(cancellationToken: TestContext.Current.CancellationToken);
                gate.Exit();

            }, cancellationToken: TestContext.Current.CancellationToken);
        }

        await Task.Delay(50, cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(other.IsCompleted);

        gate.Exit();
        await other.WaitAsync(Timeout, cancellationToken: TestContext.Current.CancellationToken);
    }

    // Starts work with a clean execution context, as an unrelated caller would have. Not async on purpose:
    // the flow suppression must not span an await.
    private static Task StartUnrelated(Func<Task> work)
    {
        using (ExecutionContext.SuppressFlow())
            return Task.Run(work);
    }

    [Fact]
    public async Task A_cancelled_wait_can_be_aborted_without_poisoning_the_caller()
    {
        var gate = new RouterGate();
        await gate.EnterAsync(cancellationToken: TestContext.Current.CancellationToken);

        using var cts = new CancellationTokenSource();
        var cancelled = StartUnrelated(async () =>
        {
            var enter = gate.EnterAsync(cts.Token);
            try { await enter; }
            catch { gate.Abort(); throw; }
        });

        await Task.Delay(50, cancellationToken: TestContext.Current.CancellationToken);
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);

        gate.Exit();
        await gate.EnterAsync(cancellationToken: TestContext.Current.CancellationToken).WaitAsync(Timeout, cancellationToken: TestContext.Current.CancellationToken);
        gate.Exit();
    }
}

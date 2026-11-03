using Routeables.Routing;

namespace Routeables.Tests.Core;

/// <summary>Tests the real <see cref="RouterGate"/> (not a copy of it).</summary>
public class RouterGateTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task Entering_twice_in_the_same_flow_throws_instead_of_waiting_forever()
    {
        var gate = new RouterGate();
        await gate.EnterAsync(cancellationToken: TestContext.Current.CancellationToken);

        try
        {
            Assert.Throws<InvalidOperationException>(() => { _ = gate.EnterAsync(cancellationToken:TestContext.Current.CancellationToken); });
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
            try { await gate.EnterAsync(); }
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

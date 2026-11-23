using Routeables.Core;

namespace Routeables.Tests.Core;

/// <summary>The versions and the gate of the selections of a menu.</summary>
public class SelectionQueueTests
{
    [Fact]
    public void Each_selection_becomes_the_newest()
    {
        var queue = new SelectionQueue();

        Assert.Equal(0, queue.Version);
        Assert.Equal(1, queue.Begin());
        Assert.Equal(2, queue.Begin());
        Assert.Equal(2, queue.Version);
    }

    [Fact]
    public void A_cancelled_newest_gives_its_place_to_the_newest_that_remains()
    {
        var queue = new SelectionQueue();
        var first = queue.Begin();
        queue.Begin(); // 2 runs or waits
        var third = queue.Begin();

        Assert.True(queue.Abandon(third));

        Assert.Equal(2, queue.Version); // not the first: the second is still live
        Assert.NotEqual(first, queue.Version);
    }

    [Fact]
    public void A_cancelled_selection_that_is_not_the_newest_changes_nothing()
    {
        var queue = new SelectionQueue();
        var first = queue.Begin();
        queue.Begin();

        Assert.False(queue.Abandon(first));

        Assert.Equal(2, queue.Version);
    }

    [Fact]
    public void When_the_only_selection_is_cancelled_the_version_goes_back_one()
    {
        var queue = new SelectionQueue();
        var only = queue.Begin();

        Assert.True(queue.Abandon(only));

        Assert.Equal(0, queue.Version);
    }

    [Fact]
    public async Task The_next_selection_runs_only_when_the_one_that_holds_the_gate_finishes()
    {
        var queue = new SelectionQueue();
        var first = queue.Begin();
        await queue.WaitAsync(TestContext.Current.CancellationToken);
        var second = queue.Begin();

        var waiting = queue.WaitAsync(TestContext.Current.CancellationToken);
        Assert.False(waiting.IsCompleted);

        queue.Finish(first);

        await waiting.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        queue.Finish(second);
    }

    [Fact]
    public async Task A_selection_cancelled_while_it_waits_for_the_gate_is_cancelled_without_taking_it()
    {
        var queue = new SelectionQueue();
        await queue.WaitAsync(TestContext.Current.CancellationToken);
        using var cancel = new CancellationTokenSource();

        var waiting = queue.WaitAsync(cancel.Token);
        await cancel.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        queue.Finish(queue.Begin()); // releases the gate that the first holds; nothing is left behind by the cancelled one
        await queue.WaitAsync(TestContext.Current.CancellationToken);
    }
}

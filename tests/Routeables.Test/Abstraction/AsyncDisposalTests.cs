using Routeables.Abstraction.Lifecycle;

namespace Routeables.Tests.Abstraction;

/// <summary>
/// A synchronous <c>Dispose</c> that meets an object that only offers <c>DisposeAsync</c> must not block: a thread that owns a
/// synchronization context (the UI thread) would deadlock, and Blazor WebAssembly, which has one thread, cannot wait at all.
/// </summary>
public class AsyncDisposalTests
{
    // The UI thread, blocked in Dispose: anything posted to it never runs.
    private sealed class BlockedContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state) { }
        public override void Send(SendOrPostCallback d, object? state) { }
    }

    private sealed class AsyncOnly(bool fail = false) : IAsyncDisposable
    {
        private int _disposed;

        public bool Disposed => Volatile.Read(ref _disposed) == 1;

        public async ValueTask DisposeAsync()
        {
            await Task.Yield(); // continues on the context it started on, if there is one
            Interlocked.Exchange(ref _disposed, 1);
            if (fail)
                throw new InvalidOperationException("dispose failed");
        }
    }

    // Its disposal waits until the test releases it. Tests of the end of a disposal need "after Dispose has returned", and a wait that needs the
    // thread pool to resume (Task.Yield) fails when the pool is busy with other tests.
    private sealed class GatedAsyncOnly(bool fail = false) : IAsyncDisposable
    {
        private readonly TaskCompletionSource _go = new(); // resumed on the thread that releases it: no pool thread needed
        private readonly TaskCompletionSource _done = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool Disposed { get; private set; }
        public Task Done => _done.Task;
        public void Go() => _go.TrySetResult();

        public async ValueTask DisposeAsync()
        {
            await _go.Task.ConfigureAwait(false);
            Disposed = true;
            _done.TrySetResult();
            if (fail)
                throw new InvalidOperationException("dispose failed");
        }
    }

    // Runs on its own thread, with the blocked context, and says whether it came back.
    private static bool ComesBack(Action action)
    {
        var task = Task.Factory.StartNew(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new BlockedContext());
            action();
        }, TaskCreationOptions.LongRunning);
        return task.Wait(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void ScopedViewCache_Dispose_does_not_block_on_an_async_only_item()
    {
        var cache = new ScopedViewCache();
        cache.GetOrCreate("k", () => new AsyncOnly());

        Assert.True(ComesBack(cache.Dispose));
    }

    [Fact]
    public void WeakDisposableTracker_Dispose_does_not_block_on_an_async_only_item()
    {
        var tracker = new WeakDisposableTracker();
        var item = new AsyncOnly();
        tracker.Track(item);

        Assert.True(ComesBack(tracker.Dispose));
        GC.KeepAlive(item);
    }

    [Fact]
    public async Task The_disposal_started_by_a_synchronous_Dispose_finishes_afterwards()
    {
        var cache = new ScopedViewCache();
        var item = (GatedAsyncOnly)cache.GetOrCreate("k", () => new GatedAsyncOnly());

        cache.Dispose(); // returns while the disposal waits: it does not block on it
        Assert.False(item.Disposed);

        item.Go();
        await item.Done.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        Assert.True(item.Disposed);
    }

    [Fact]
    public async Task A_failing_async_disposal_is_reported_not_thrown_by_Dispose()
    {
        var cache = new ScopedViewCache();
        var item = (GatedAsyncOnly)cache.GetOrCreate("k", () => new GatedAsyncOnly(fail: true));

        var thrown = Record.Exception(cache.Dispose);

        Assert.Null(thrown);
        item.Go();
        await item.Done.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        Assert.True(item.Disposed);
    }
}

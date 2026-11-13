using Routeables.Abstraction;
using Routeables.Abstraction.Lifecycle;

namespace Routeables.Tests.Abstraction;

/// <summary>
/// A synchronous <c>Dispose</c> that meets an object that only offers <c>DisposeAsync</c> must not block: a thread that owns a
/// synchronization context (the UI thread) would deadlock, and Blazor WebAssembly, which has one thread, cannot wait at all.
/// </summary>
public class AsyncDisposalTests
{
    // The UI thread, blocked in Dispose: whatever is posted to it never runs.
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
            await Task.Yield();   // continues on the context it was started on, when there is one
            Interlocked.Exchange(ref _disposed, 1);
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

    // Polls without blocking the thread: on a host whose test thread owns a synchronization context (a device test runner), the continuation of
    // DisposeAsync would be posted to the very thread that waits.
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(10);
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
        // Off the context of the test thread, so the disposal can continue whatever the host is.
        var (cache, item) = await Task.Run(() =>
        {
            var cache = new ScopedViewCache();
            var item = (AsyncOnly)cache.GetOrCreate("k", () => new AsyncOnly());
            cache.Dispose();
            return (cache, item);
        });

        await WaitUntilAsync(() => item.Disposed);
        Assert.True(item.Disposed);
        GC.KeepAlive(cache);
    }

    [Fact]
    public async Task A_failing_async_disposal_is_reported_not_thrown_by_Dispose()
    {
        var (thrown, item) = await Task.Run(() =>
        {
            var cache = new ScopedViewCache();
            var item = (AsyncOnly)cache.GetOrCreate("k", () => new AsyncOnly(fail: true));

            Exception? thrown = null;
            try { cache.Dispose(); }
            catch (Exception ex) { thrown = ex; }
            return (thrown, item);
        });

        Assert.Null(thrown);
        await WaitUntilAsync(() => item.Disposed);
        Assert.True(item.Disposed);
    }
}

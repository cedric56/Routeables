using Routeables.Abstraction;

namespace Routeables.Tests.Abstraction;

public class WeakDisposableTrackerTests
{
    [Fact]
    public void Dispose_disposes_tracked_instances_once_and_continues_after_an_instance_throws()
    {
        var first = new DisposableProbe();
        var failing = new DisposableProbe(throwOnDispose: true);
        var last = new DisposableProbe();
        using var tracker = new WeakDisposableTracker();
        tracker.Track(first);
        tracker.Track(failing);
        tracker.Track(last);
        tracker.Track(new object());

        tracker.Dispose();
        tracker.Dispose();

        Assert.Equal(1, first.DisposeCount);
        Assert.Equal(1, failing.DisposeCount);
        Assert.Equal(1, last.DisposeCount);
    }

    [Fact]
    public void Track_ignores_null_and_non_disposable_instances()
    {
        using var tracker = new WeakDisposableTracker();

        tracker.Track(null);
        tracker.Track(new object());

        tracker.Dispose();
    }

    [Fact]
    public void Collected_instances_do_not_pile_up_in_the_tracker()
    {
        using var tracker = new WeakDisposableTracker();

        for (var round = 0; round < 20; round++)
        {
            TrackShortLived(tracker, 100);      // pages shown and gone, as in an application that stays open for days
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        Assert.True(tracker.Count < 500, $"{tracker.Count} references are still held for 2000 pages that were collected");
    }

    [Fact]
    public void Nothing_is_tracked_after_the_tracker_is_disposed()
    {
        var tracker = new WeakDisposableTracker();
        tracker.Dispose();

        tracker.Track(new DisposableProbe());

        Assert.Equal(0, tracker.Count);
    }

    // Not inlined, so that nothing keeps the instances alive once the method has returned.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void TrackShortLived(WeakDisposableTracker tracker, int count)
    {
        for (var i = 0; i < count; i++)
            tracker.Track(new DisposableProbe());
    }

    [Fact]
    public void Dispose_also_disposes_instances_that_only_implement_IAsyncDisposable()
    {
        var probe = new AsyncOnlyProbe();
        var tracker = new WeakDisposableTracker();
        tracker.Track(probe);

        tracker.Dispose();

        Assert.Equal(1, probe.DisposeCount);
    }

    [Fact]
    public async Task DisposeAsync_prefers_IAsyncDisposable_and_disposes_only_once()
    {
        var asyncOnly = new AsyncOnlyProbe();
        var sync = new DisposableProbe();
        var tracker = new WeakDisposableTracker();
        tracker.Track(asyncOnly);
        tracker.Track(sync);

        await tracker.DisposeAsync();
        tracker.Dispose();

        Assert.Equal(1, asyncOnly.DisposeCount);
        Assert.Equal(1, sync.DisposeCount);
    }

    private sealed class AsyncOnlyProbe : IAsyncDisposable
    {
        public int DisposeCount { get; private set; }
        public ValueTask DisposeAsync() { DisposeCount++; return ValueTask.CompletedTask; }
    }

    private sealed class DisposableProbe(bool throwOnDispose = false) : IDisposable
    {
        public int DisposeCount { get; private set; }

        public void Dispose()
        {
            DisposeCount++;
            if (throwOnDispose)
                throw new InvalidOperationException("dispose failed");
        }
    }
}

using Routeables.Abstraction;
using Routeables.Abstraction.Lifecycle;

namespace Routeables.Test.Abstraction;

public class ScopedViewCacheTests
{
    private sealed class Item : IDisposable
    {
        public int Disposed { get; private set; }
        public void Dispose() => Disposed++;
    }

    [Fact]
    public void The_factory_runs_once_per_key()
    {
        var cache = new ScopedViewCache();
        var calls = 0;

        var first = cache.GetOrCreate("k", () => { calls++; return new object(); });
        var second = cache.GetOrCreate("k", () => { calls++; return new object(); });

        Assert.Same(first, second);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Concurrent_callers_receive_the_same_instance()
    {
        var cache = new ScopedViewCache();
        var calls = 0;

        var results = await Task.WhenAll(Enumerable.Range(0, 64).Select(_ => Task.Run(() =>
            cache.GetOrCreate("k", () => { Interlocked.Increment(ref calls); return new object(); }))));

        Assert.Equal(1, calls);
        Assert.All(results, r => Assert.Same(results[0], r));
    }

    [Fact]
    public void Dispose_disposes_what_the_cache_created_and_rejects_later_use()
    {
        var cache = new ScopedViewCache();
        var item = (Item)cache.GetOrCreate("k", () => new Item());

        cache.Dispose();
        cache.Dispose();

        Assert.Equal(1, item.Disposed);
        Assert.Throws<ObjectDisposedException>(() => cache.GetOrCreate("k", () => new object()));
    }

    [Fact]
    public async Task DisposeAsync_disposes_the_cached_items()
    {
        var cache = new ScopedViewCache();
        var item = (Item)cache.GetOrCreate("k", () => new Item());

        await cache.DisposeAsync();

        Assert.Equal(1, item.Disposed);
    }

    [Fact]
    public void A_factory_that_waits_for_another_thread_that_asks_the_cache_does_not_block_it()
    {
        // A constructor that hands work to another thread, which uses the cache for another key, and waits for it.
        var cache = new ScopedViewCache();
        object? other = null;
        var owned = false;

        cache.GetOrCreate("k", () =>
        {
            var work = Task.Run(() => { other = cache.GetOrCreate("other", () => new object()); owned = cache.Owns(other); });
            Assert.True(work.Wait(TimeSpan.FromSeconds(10)), "the other thread was blocked by the lock of the cache");
            return new object();
        });

        Assert.NotNull(other);
        Assert.True(owned);
    }

    [Fact]
    public void A_factory_that_throws_leaves_nothing_in_the_cache_and_is_asked_again()
    {
        var cache = new ScopedViewCache();

        Assert.Throws<InvalidOperationException>(() => cache.GetOrCreate("k", () => throw new InvalidOperationException("boom")));
        var built = cache.GetOrCreate("k", () => new object());

        Assert.True(cache.Owns(built));
    }

    [Fact]
    public void A_cache_disposed_while_a_page_is_built_disposes_that_page()
    {
        var cache = new ScopedViewCache();
        var item = new Item();

        Assert.Throws<ObjectDisposedException>(() => cache.GetOrCreate("k", () => { cache.Dispose(); return item; }));

        Assert.Equal(1, item.Disposed);
    }

    private sealed class Failing : IDisposable
    {
        public void Dispose() => FailHere();
        private static void FailHere() => throw new InvalidOperationException("dispose failed");
    }

    [Fact]
    public void A_single_failure_to_dispose_keeps_its_own_stack()
    {
        var cache = new ScopedViewCache();
        cache.GetOrCreate("k", () => new Failing());

        var ex = Assert.Throws<InvalidOperationException>(cache.Dispose);

        Assert.Contains("FailHere", ex.StackTrace);
    }
}

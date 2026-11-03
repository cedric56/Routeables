using Routeables.Abstraction;

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
}

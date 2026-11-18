namespace Routeables.Abstraction.Lifecycle;

/// <summary>
/// Keeps what a registration created, by key, for as long as the cache lives, and disposes it with the cache.
/// Scoped: one per scope (<see cref="ScopedViewCache"/>). Singleton: one per provider (<see cref="SingletonViewCache"/>).
/// </summary>
internal class ScopedViewCache : IDisposable, IAsyncDisposable
{
    // One entry per key: the factory (code of the application: a constructor) runs under the lock of its own entry, never under the one of the
    // cache, so that a constructor that waits for another thread which asks this cache for something else does not block it for ever.
    private sealed class Entry { public readonly object Gate = new(); public object? Value; }

    private readonly Dictionary<object, Entry> _cache = new();
    private readonly object _gate = new();
    private bool _disposed;

    public object GetOrCreate(object key, Func<object> factory)
    {
        Entry entry;
        // A plain Dictionary is not safe for concurrent reads and writes: every access takes the lock.
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (!_cache.TryGetValue(key, out entry!))
                _cache[key] = entry = new Entry();
            else if (entry.Value is { } existing)
                return existing;
        }

        lock (entry.Gate)
        {
            if (entry.Value is { } built)
                return built;       // another caller built it while this one waited for the entry

            // A failure keeps the entry: a caller that waited on it builds the page under the same lock, and what it builds stays in the cache
            // (removing the entry would leave that caller with a page the cache no longer owns).
            var created = factory();

            lock (_gate)
            {
                if (!_disposed)
                {
                    entry.Value = created;
                    return created;
                }
            }

            // The cache was disposed while the page was being built: nobody will dispose it but this.
            if (created is IDisposable disposable) disposable.Dispose();
            else if (created is IAsyncDisposable asyncDisposable) AsyncDisposal.Start<ScopedViewCache>(asyncDisposable);
            throw new ObjectDisposedException(GetType().Name);
        }
    }

    /// <summary>Whether this cache created <paramref name="instance"/>, and so will dispose it and may hand it out again.</summary>
    public bool Owns(object instance)
    {
        lock (_gate)
            return _cache.Values.Any(v => ReferenceEquals(v.Value, instance));
    }

    public void Dispose()
    {
        List<Exception>? failures = null;
        foreach (var item in Drain())
        {
            // One view that fails to dispose does not keep the others alive.
            try
            {
                if (item is IDisposable disposable) disposable.Dispose();
                else if (item is IAsyncDisposable asyncDisposable) AsyncDisposal.Start<ScopedViewCache>(asyncDisposable);
            }
            catch (Exception ex) { (failures ??= []).Add(ex); }
        }
        Raise(failures);
    }

    private static void Raise(List<Exception>? failures)
    {
        if (failures is null)
            return;
        if (failures.Count == 1)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failures[0]).Throw();     // with its own stack, not the one of this method
        throw new AggregateException(failures);
    }

    public async ValueTask DisposeAsync()
    {
        List<Exception>? failures = null;
        foreach (var item in Drain())
        {
            try
            {
                if (item is IAsyncDisposable asyncDisposable) await asyncDisposable.DisposeAsync();
                else if (item is IDisposable disposable) disposable.Dispose();
            }
            catch (Exception ex) { (failures ??= []).Add(ex); }
        }
        Raise(failures);
    }

    // The cache created these views and view models (outside the container's tracking), so it owns them.
    private List<object> Drain()
    {
        lock (_gate)
        {
            if (_disposed) return [];
            _disposed = true;
            var items = _cache.Values.Select(entry => entry.Value).OfType<object>().ToList();
            _cache.Clear();
            return items;
        }
    }
}


/// <summary>The same cache, registered as a singleton: the views and view models of the singleton registrations of one provider.</summary>
internal sealed class SingletonViewCache : ScopedViewCache;

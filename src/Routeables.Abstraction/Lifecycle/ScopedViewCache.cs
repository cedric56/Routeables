namespace Routeables.Abstraction.Lifecycle;

/// <summary>
/// Keeps what a registration created, by key, for as long as the cache lives, and disposes it with the cache.
/// Scoped: one per scope (<see cref="ScopedViewCache"/>). Singleton: one per provider (<see cref="SingletonViewCache"/>).
/// </summary>
internal class ScopedViewCache : IDisposable, IAsyncDisposable
{
    private readonly Dictionary<object, object> _cache = new();
    private readonly object _gate = new();
    private bool _disposed;

    public object GetOrCreate(object key, Func<object> factory)
    {
        // A plain Dictionary is not safe for concurrent reads and writes: every access takes the lock.
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_cache.TryGetValue(key, out var existing))
                return existing;

            return _cache[key] = factory();
        }
    }

    /// <summary>Whether this cache created <paramref name="instance"/>, and so will dispose it and may hand it out again.</summary>
    public bool Owns(object instance)
    {
        lock (_gate)
            return _cache.Values.Any(v => ReferenceEquals(v, instance));
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
        if (failures is not null)
            throw failures.Count == 1 ? failures[0] : new AggregateException(failures);
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
            var items = _cache.Values.ToList();
            _cache.Clear();
            return items;
        }
    }
}


/// <summary>The same cache, registered as a singleton: the views and view models of the singleton registrations of one provider.</summary>
internal sealed class SingletonViewCache : ScopedViewCache;

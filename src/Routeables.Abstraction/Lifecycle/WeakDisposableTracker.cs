using Routeables.Abstraction.Errors;

namespace Routeables.Abstraction.Lifecycle;

internal sealed class WeakDisposableTracker : IDisposable, IAsyncDisposable
{
    private readonly List<WeakReference> _refs = new();
    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<object, object> _known = new();
    // What the router disposed already when the page left the history: the end of the scope must not dispose it a second time.
    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<object, object> _gone = new();
    private bool _disposed;
    private int _pruneAt = 64;

    /// <summary>The number of references held, dead ones included (for tests).</summary>
    internal int Count
    {
        get { lock (_refs) return _refs.Count; }
    }

    /// <summary>Says that <paramref name="instance"/> was disposed by someone else: the end of the scope leaves it alone.</summary>
    public void Forget(object? instance)
    {
        if (instance is not null)
            _gone.TryAdd(instance, instance);
    }

    public void Track(object? instance)
    {
        if (instance is IDisposable or IAsyncDisposable)
        {
            lock (_refs)
            {
                if (_disposed)
                    return;

                // The same page is resolved again on every route change: tracked once, disposed once.
                if (!_known.TryAdd(instance, instance))
                    return;

                // The scope of a desktop or mobile application lasts as long as the application: without this, every
                // transient page ever shown would leave a WeakReference (and its handle) in the list for ever.
                if (_refs.Count >= _pruneAt)
                {
                    _refs.RemoveAll(weak => weak.Target is null);
                    _pruneAt = Math.Max(64, _refs.Count * 2);
                }

                _refs.Add(new WeakReference(instance));
            }
        }
    }

    public void Dispose()
    {
        foreach (var item in Drain())
        {
            try
            {
                if (item is IDisposable d) d.Dispose();
                else if (item is IAsyncDisposable a) AsyncDisposal.Start<WeakDisposableTracker>(a);
            }
            catch (Exception ex) { RouteableErrors.Report<WeakDisposableTracker>(null, ex); }
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var item in Drain())
        {
            try
            {
                if (item is IAsyncDisposable a) await a.DisposeAsync();
                else if (item is IDisposable d) d.Dispose();
            }
            catch (Exception ex) { RouteableErrors.Report<WeakDisposableTracker>(null, ex); }
        }
    }

    // Takes the live objects out under the lock and disposes them outside it: user code never runs while it is held.
    private List<object> Drain()
    {
        lock (_refs)
        {
            if (_disposed) return [];
            _disposed = true;
            var items = _refs.Select(w => w.Target).OfType<object>().Where(item => !_gone.TryGetValue(item, out _)).ToList();
            _refs.Clear();
            return items;
        }
    }
}

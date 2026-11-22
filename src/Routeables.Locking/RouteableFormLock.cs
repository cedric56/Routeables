using Routeables.Abstraction.Locking;

namespace Routeables.Locking;

/// <summary>
/// <see cref="IRouteableFormLock"/> over the broker: the first lock publishes a lock message and the last release publishes the unlock,
/// whatever the number of forms in between.
/// </summary>
internal sealed class RouteableFormLock(IRouteableLockBroker broker) : IRouteableFormLock
{
    // The count and the queue of messages are changed under the gate; the subscribers are called outside it. A subscriber that hands its work to the
    // thread of the interface and waits for it would otherwise block that thread when it asks Lock() or IsLocked meanwhile.
    private readonly object _gate = new();
    private readonly Queue<bool> _pending = new(); // the messages in the order they happened: true locks, false unlocks
    private bool _draining;
    private int _holders;

    public bool IsLocked => Volatile.Read(ref _holders) > 0;

    public IDisposable Lock()
    {
        lock (_gate)
        {
            if (++_holders == 1)
                _pending.Enqueue(true);
        }

        try
        {
            Drain();
        }
        catch
        {
            // A subscriber that throws leaves no token to give back: take the lock back, or the form would stay locked forever.
            lock (_gate)
            {
                _holders = 0;
                _pending.Enqueue(false);
            }

            try { Drain(); }
            catch { /* the subscribers that were told are told again; the first failure is the one raised */ }
            throw;
        }

        return new Token(this);
    }

    private void Release()
    {
        lock (_gate)
        {
            if (--_holders == 0)
                _pending.Enqueue(false);
        }

        Drain();
    }

    // One thread at a time delivers the messages, in order, until none is left; a thread that finds another one delivering leaves its message
    // to it. A lock and an unlock reach the subscribers in the order they happened, and no thread waits for another while holding the gate.
    private void Drain()
    {
        while (true)
        {
            bool isLocked;
            lock (_gate)
            {
                if (_draining || _pending.Count == 0)
                    return;
                _draining = true;
                isLocked = _pending.Dequeue();
            }

            try
            {
                broker.Publish(new RouteableLockMessage(IsLocked: isLocked, IsAllLocked: false));
            }
            catch when (!isLocked)
            {
                // An unlock that failed: the ones queued behind it are still delivered, then the failure is raised.
                lock (_gate)
                    _draining = false;
                Drain();
                throw;
            }
            finally
            {
                lock (_gate)
                    _draining = false;
            }
        }
    }

    private sealed class Token(RouteableFormLock owner) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
                owner.Release();
        }
    }
}

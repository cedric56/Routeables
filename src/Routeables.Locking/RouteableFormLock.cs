using Routeables.Abstraction.Locking;

namespace Routeables.Locking;

/// <summary>
/// <see cref="IRouteableFormLock"/> over the broker: the first lock publishes a lock message and the last release publishes the unlock,
/// whatever the number of forms in between.
/// </summary>
internal sealed class RouteableFormLock(IRouteableLockBroker broker) : IRouteableFormLock
{
    private readonly object _gate = new();
    private int _holders;

    public bool IsLocked
    {
        get { lock (_gate) return _holders > 0; }
    }

    public IDisposable Lock()
    {
        // Published inside the gate: a lock and an unlock reach the subscribers in the order they happened.
        lock (_gate)
        {
            if (++_holders == 1)
            {
                try
                {
                    broker.Publish(new RouteableLockMessage(IsLocked: true, IsAllLocked: false));
                }
                catch
                {
                    // A subscriber that throws leaves no token to give back: the lock is taken back, or the form would stay locked for ever.
                    _holders = 0;
                    try { broker.Publish(new RouteableLockMessage(IsLocked: false, IsAllLocked: false)); }
                    catch { /* the subscribers that were told are told again; the first failure is the one raised */ }
                    throw;
                }
            }
        }

        return new Token(this);
    }

    private void Release()
    {
        lock (_gate)
        {
            if (--_holders == 0)
                broker.Publish(new RouteableLockMessage(IsLocked: false, IsAllLocked: false));
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

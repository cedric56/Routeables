using Routeables.Abstraction.Errors;
using Routeables.Abstraction.Locking;

namespace Routeables.Locking;

/// <summary>
/// <see cref="IRouteableFormLock"/> over the broker: the first lock publishes a lock message and the last release publishes the unlock,
/// whatever the number of forms in between.
/// </summary>
internal sealed class RouteableFormLock(IRouteableLockBroker broker) : IRouteableFormLock
{
    // The count and the queue are changed under the gate; the subscribers are called outside it. A subscriber that hands its work to the UI
    // thread and waits for it would otherwise block that thread when it calls Lock() or IsLocked meanwhile.
    private readonly object _gate = new();
    private readonly Queue<(bool IsLocked, object Owner)> _pending = new(); // the messages in the order they happened: true locks, false unlocks, and who asked
    private bool _draining;
    private int _holders;

    public bool IsLocked => Volatile.Read(ref _holders) > 0;

    public IDisposable Lock()
    {
        object? mine = null;
        lock (_gate)
        {
            if (++_holders == 1)
                _pending.Enqueue((true, mine = new object()));
        }

        try
        {
            Drain(mine);
        }
        catch
        {
            // A subscriber that throws leaves no token to give back: take the lock back, or the form would stay locked forever.
            object? undo = null;
            lock (_gate)
            {
                // Only this call's hold: another form that locked meanwhile keeps its own.
                if (--_holders == 0)
                    _pending.Enqueue((false, undo = new object()));
            }

            try { Drain(undo); }
            catch { /* the subscribers that were told are told again; the first failure is the one raised */ }
            throw;
        }

        return new Token(this);
    }

    private void Release()
    {
        object? mine = null;
        lock (_gate)
        {
            if (--_holders == 0)
                _pending.Enqueue((false, mine = new object()));
        }

        Drain(mine);
    }

    // One thread at a time delivers the messages, in order, until none is left; a thread that finds another one delivering leaves its message
    // to it. A lock and an unlock reach the subscribers in the order they happened, and no thread waits for another while holding the gate.
    // A subscriber that throws on a message another call asked for does not fail this call: the error is reported, and the one who asked
    // does not learn of it (it is not waiting). The failure of the caller's own message is raised.
    private void Drain(object? mine)
    {
        Exception? failure = null;
        while (true)
        {
            bool isLocked;
            object owner;
            lock (_gate)
            {
                if (_draining || _pending.Count == 0)
                    break;
                _draining = true;
                (isLocked, owner) = _pending.Dequeue();
            }

            var own = mine is not null && ReferenceEquals(owner, mine);
            try
            {
                broker.Publish(new RouteableLockMessage(IsLocked: isLocked, IsAllLocked: false));
            }
            catch (Exception ex) when (!own)
            {
                RouteableErrors.Report(null, "RouteableFormLock.Drain", ex);
            }
            catch (Exception ex) when (!isLocked)
            {
                // The caller's own unlock failed: the ones queued behind it are still delivered, then the failure is raised.
                failure ??= ex;
            }
            finally
            {
                lock (_gate)
                    _draining = false;
            }
        }

        if (failure is not null)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
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

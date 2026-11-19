using Routeables.Abstraction;

namespace Routeables.Routing;

/// <summary>
/// Provides asynchronous reentrancy protection and serialization for routing operations per router instance
/// using a semaphore combined with an <see cref="AsyncLocal{T}"/> token of the operation in progress.
/// </summary>
internal sealed class RouterGate(RouterConcurrency concurrency = RouterConcurrency.Queue)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    // CancelCurrent only. The source the operation in progress observes (linked to its caller's token), and the number of the latest caller.
    // A caller that is no longer the latest when its turn comes has been replaced by a newer one, and does not start.
    private CancellationTokenSource? _running;
    private long _latest;
    // The numbers of the callers that still want to run (waiting for the gate, or entering). A caller cancelled by its own token stops wanting
    // to run and no longer replaces the ones before it, so "the last one wins" means the last one still wanted.
    private readonly SortedSet<long> _wanted = [];
    // The token of the operation that holds the gate, and the one each flow carries. A flow is reentrant only while the token it carries is the
    // one in progress. Work started without waiting (Task.Run, FireAndForget) copies the token of the operation then running and keeps it
    // forever, so a boolean would treat every later navigation from that work as reentrant.
    private readonly AsyncLocal<object?> _inProgress = new();
    private object? _current;

    // The token of an operation, with the "detached" mark of the flow that took the gate.
    private sealed class Pass(object? detached)
    {
        public object? Detached { get; } = detached;
    }

    /// <summary>
    /// Asynchronously enters the route change gate, throwing an exception if reentrant routing is attempted.
    /// </summary>
    /// <remarks>
    /// Intentionally NOT <c>async</c>. A value written to an <see cref="AsyncLocal{T}"/> inside an async
    /// method is discarded when that method returns to its caller, so the flag must be set synchronously
    /// in the caller's own flow for the callbacks it awaits (a nested route change) to see it. Callers
    /// must call <see cref="Abort"/> if awaiting the returned task fails.
    /// </remarks>
    /// <param name="cancellationToken">A token to observe while waiting to enter the gate.</param>
    /// <returns>A task that completes when the gate has been successfully entered.</returns>
    /// <exception cref="InvalidOperationException">Thrown if reentrant routing is detected.</exception>
    public Task EnterAsync(CancellationToken cancellationToken = default)
    {
        // Nested: the flow carries the token of the operation in progress, and has not been detached (FireAndForget) from the flow that took it.
        if (_inProgress.Value is Pass carried && ReferenceEquals(carried, Volatile.Read(ref _current))
            && ReferenceEquals(carried.Detached, RouteableHelper.DetachedFlow.Value))
            throw new InvalidOperationException("Reentrant route changes are not supported.");

        // Re-evaluating the guards (RefreshGuardsOn) never replaces the navigation in progress: it waits for it.
        var cancelCurrent = concurrency == RouterConcurrency.CancelCurrent && RouteableHelper.QuietFlow.Value is null;
        var number = 0L;
        if (cancelCurrent)
        {
            cancellationToken.ThrowIfCancellationRequested(); // already cancelled: it replaces nobody
            number = Want();
        }

        // Take the place in the queue before asking the operation in progress to stop. That operation can end as soon as it is asked (its
        // cancellation runs the continuation inline), release the gate, and let this caller in before the callers that come after it have said
        // they want to replace it. By waiting first, the gate reaches this caller through the semaphore, whose waiters resume separately.
        Task wait;
        try { wait = _gate.WaitAsync(cancellationToken); }
        catch { Unwant(number); throw; }
        var immediate = wait.IsCompletedSuccessfully; // the gate was free: nothing to wait for and nothing to replace
        if (cancelCurrent)
        {
            try { Volatile.Read(ref _running)?.Cancel(); }
            catch (ObjectDisposedException) { } // it finished at the same time
        }

        var token = new Pass(RouteableHelper.DetachedFlow.Value);
        _inProgress.Value = token;
        if (immediate)
        {
            Begin(token, cancelCurrent, cancellationToken, number);
            return wait;
        }

        return Acquire(wait, token, cancelCurrent, number, cancellationToken);
    }

    private long Want()
    {
        lock (_wanted)
        {
            var number = ++_latest;
            _wanted.Add(number);
            return number;
        }
    }

    private void Unwant(long number)
    {
        if (number == 0)
            return;
        lock (_wanted)
            _wanted.Remove(number);
    }

    private bool Replaced(long number)
    {
        lock (_wanted)
            return _wanted.Count > 0 && _wanted.Max > number;
    }

    // For tests: runs where a newer caller can come in, once the gate is held and before the operation is published.
    internal Action? BeforeBegin;

    private void Begin(object token, bool cancelCurrent, CancellationToken caller, long number)
    {
        BeforeBegin?.Invoke();
        if (cancelCurrent)
        {
            var source = CancellationTokenSource.CreateLinkedTokenSource(caller);
            Interlocked.Exchange(ref _running, source); // a full fence: published before the wanted numbers are read below
            // A newer caller that came in after the wait and before this point found no source to cancel (it is only published now). It asked for
            // this operation to be replaced, so cancel it at once. Whichever of the two runs second sees the other.
            if (Replaced(number))
                source.Cancel();
            Unwant(number);
        }

        Volatile.Write(ref _current, token);
    }

    /// <summary>
    /// The token the operation that just entered must observe: its caller's token, and under
    /// <see cref="RouterConcurrency.CancelCurrent"/> also the cancellation a newer route change asks for.
    /// </summary>
    public CancellationToken Observe(CancellationToken caller) => Volatile.Read(ref _running)?.Token ?? caller;

    // The operation holds the gate only once the wait is over: another caller that is waiting must not take its place as the operation in progress.
    private async Task Acquire(Task wait, object token, bool cancelCurrent, long number, CancellationToken caller)
    {
        try { await wait.ConfigureAwait(false); }
        catch { Unwant(number); throw; } // cancelled by its own caller while waiting: the callers before it are no longer replaced by it

        // The gate can already be ours when the operation in progress ended as soon as it was asked to (its cancellation runs inline). The caller
        // gets its task back and may start the navigations that replace this one before this one starts, so it takes its turn after them.
        if (cancelCurrent)
            await Task.Yield();

        if (cancelCurrent && Replaced(number))
        {
            // A newer navigation came while this one waited: it replaces this one, which never starts.
            Unwant(number);
            _gate.Release();
            throw new OperationCanceledException("A newer route change replaced this one.");
        }

        Begin(token, cancelCurrent, caller, number);
    }

    /// <summary>
    /// Exits the route change gate, clearing the in-progress token and releasing the underlying semaphore.
    /// </summary>
    public void Exit()
    {
        Volatile.Write(ref _current, null);
        _inProgress.Value = null;
        Interlocked.Exchange(ref _running, null)?.Dispose();
        _gate.Release();
    }

    /// <summary>
    /// Clears the in-progress token after a failed (for example, cancelled) <see cref="EnterAsync"/> operation.
    /// </summary>
    public void Abort() => _inProgress.Value = null;
}
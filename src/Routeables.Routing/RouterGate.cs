using Routeables.Abstraction;

namespace Routeables.Routing;

/// <summary>
/// Provides asynchronous reentrancy protection and serialization for routing operations per router instance
/// using a semaphore combined with an <see cref="AsyncLocal{T}"/> token of the operation in progress.
/// </summary>
internal sealed class RouterGate(RouterConcurrency concurrency = RouterConcurrency.Queue)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    // CancelCurrent only. The source the operation in progress observes (linked to its caller's token), and the number of the latest
    // caller: a caller that is no longer the latest when its turn comes has been replaced by a newer one and does not start.
    private CancellationTokenSource? _running;
    private long _latest;
    // The numbers of the callers that still want to run (waiting for the gate, or entering): one that is cancelled by its own caller stops
    // wanting to, and no longer replaces the ones before it ("the last one wins" is the last one that is still wanted).
    private readonly SortedSet<long> _wanted = [];
    // The token of the operation that holds the gate, and the one each flow carries. A flow is reentrant only while the token it carries is
    // the one in progress: work started without waiting (Task.Run, FireAndForget) copies the token of the operation that was running, and
    // keeps it for ever, so a boolean would call every later route change from that work "reentrant".
    private readonly AsyncLocal<object?> _inProgress = new();
    private object? _current;

    // The token of an operation, with the detached mark of the flow that took the gate.
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
        // Nested: the flow carries the token of the operation in progress, and was not detached since (FireAndForget) from the flow that took it.
        if (_inProgress.Value is Pass carried && ReferenceEquals(carried, Volatile.Read(ref _current))
            && ReferenceEquals(carried.Detached, RouteableHelper.DetachedFlow.Value))
            throw new InvalidOperationException("Reentrant route changes are not supported.");

        var cancelCurrent = concurrency == RouterConcurrency.CancelCurrent;
        var number = 0L;
        if (cancelCurrent)
        {
            cancellationToken.ThrowIfCancellationRequested();   // already cancelled: it replaces nobody
            number = Want();
            try { Volatile.Read(ref _running)?.Cancel(); }
            catch (ObjectDisposedException) { }   // it finished at the same time
        }

        Task wait;
        try { wait = _gate.WaitAsync(cancellationToken); }
        catch { Unwant(number); throw; }
        var token = new Pass(RouteableHelper.DetachedFlow.Value);
        _inProgress.Value = token;
        if (wait.IsCompletedSuccessfully)
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

    // For the tests: runs where a newer caller can come in, once the gate is held and before the operation is published.
    internal Action? BeforeBegin;

    private void Begin(object token, bool cancelCurrent, CancellationToken caller, long number)
    {
        BeforeBegin?.Invoke();
        if (cancelCurrent)
        {
            var source = CancellationTokenSource.CreateLinkedTokenSource(caller);
            Interlocked.Exchange(ref _running, source);     // a full fence: published before the wanted numbers are read below
            // A newer caller that came after the wait and before this point found no source to cancel (it is published only now): it asked for
            // this one to be replaced, so it is cancelled at once. Whichever of the two goes second sees the other.
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

    // The operation holds the gate only once the wait is over: another caller that waits must not take its place as the one in progress.
    private async Task Acquire(Task wait, object token, bool cancelCurrent, long number, CancellationToken caller)
    {
        try { await wait.ConfigureAwait(false); }
        catch { Unwant(number); throw; }     // cancelled by its own caller while it waited: the ones before it are no longer replaced by it

        if (cancelCurrent && Replaced(number))
        {
            // A newer route change came while this one waited: it replaces this one, which never starts.
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
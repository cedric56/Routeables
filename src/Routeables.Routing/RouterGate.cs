namespace Routeables.Routing;

/// <summary>
/// Provides asynchronous reentrancy protection and serialization for routing operations per router instance
/// using a semaphore combined with an <see cref="AsyncLocal{T}"/> token of the operation in progress.
/// </summary>
internal sealed class RouterGate
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    // The token of the operation that holds the gate, and the one each flow carries. A flow is reentrant only while the token it carries is
    // the one in progress: work started without waiting (Task.Run, FireAndForget) copies the token of the operation that was running, and
    // keeps it for ever, so a boolean would call every later route change from that work "reentrant".
    private readonly AsyncLocal<object?> _inProgress = new();
    private object? _current;

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
        if (_inProgress.Value is { } carried && ReferenceEquals(carried, Volatile.Read(ref _current)))
            throw new InvalidOperationException("Reentrant route changes are not supported.");

        var wait = _gate.WaitAsync(cancellationToken);
        var token = new object();
        _inProgress.Value = token;
        if (wait.IsCompletedSuccessfully)
        {
            Volatile.Write(ref _current, token);
            return wait;
        }

        return Acquire(wait, token);
    }

    // The operation holds the gate only once the wait is over: another caller that waits must not take its place as the one in progress.
    private async Task Acquire(Task wait, object token)
    {
        await wait.ConfigureAwait(false);
        Volatile.Write(ref _current, token);
    }

    /// <summary>
    /// Exits the route change gate, clearing the in-progress token and releasing the underlying semaphore.
    /// </summary>
    public void Exit()
    {
        Volatile.Write(ref _current, null);
        _inProgress.Value = null;
        _gate.Release();
    }

    /// <summary>
    /// Clears the in-progress token after a failed (for example, cancelled) <see cref="EnterAsync"/> operation.
    /// </summary>
    public void Abort() => _inProgress.Value = null;
}
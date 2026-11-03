namespace Routeables.Routing;

/// <summary>
/// Provides asynchronous reentrancy protection and serialization for routing operations per router instance
/// using a semaphore combined with an <see cref="AsyncLocal{T}"/> flag.
/// </summary>
internal sealed class RouterGate
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly AsyncLocal<bool> _inProgress = new();

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
        if (_inProgress.Value)
            throw new InvalidOperationException("Reentrant route changes are not supported.");

        var wait = _gate.WaitAsync(cancellationToken);
        _inProgress.Value = true;
        return wait;
    }

    /// <summary>
    /// Exits the route change gate, resetting the in-progress flag and releasing the underlying semaphore.
    /// </summary>
    public void Exit()
    {
        _inProgress.Value = false;
        _gate.Release();
    }

    /// <summary>
    /// Clears the in-progress flag after a failed (for example, cancelled) <see cref="EnterAsync"/> operation.
    /// </summary>
    public void Abort() => _inProgress.Value = false;
}
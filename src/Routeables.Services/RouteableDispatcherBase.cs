namespace Routeables.Services;

/// <summary>
/// The part of <see cref="IRouteableDispatcher"/> that is the same on every platform: a host says whether the calling thread is the
/// one of the interface (<see cref="CheckAccess"/>) and how to queue work on it (<see cref="Post"/>); the rest is built on those two.
/// </summary>
public abstract class RouteableDispatcherBase : IRouteableDispatcher
{
    /// <inheritdoc />
    public abstract bool CheckAccess();

    /// <inheritdoc />
    public abstract void Post(Action action);

    /// <inheritdoc />
    public Task InvokeAsync(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return InvokeAsync(() => { action(); return true; });
    }

    /// <inheritdoc />
    public Task<T> InvokeAsync<T>(Func<T> function)
    {
        ArgumentNullException.ThrowIfNull(function);

        if (CheckAccess())
        {
            try { return Task.FromResult(function()); }
            catch (Exception ex) { return Task.FromException<T>(ex); }
        }

        // The caller waits for the task, not for the thread: the work is queued and its outcome, exception included, is handed back.
        var done = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        Post(() =>
        {
            try { done.SetResult(function()); }
            catch (Exception ex) { done.SetException(ex); }
        });
        return done.Task;
    }

    /// <inheritdoc />
    public Task InvokeAsync(Func<Task> function)
    {
        ArgumentNullException.ThrowIfNull(function);

        if (CheckAccess())
        {
            try { return function(); }
            catch (Exception ex) { return Task.FromException(ex); }
        }

        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Post(async () =>
        {
            try { await function(); done.SetResult(); }
            catch (Exception ex) { done.SetException(ex); }
        });
        return done.Task;
    }
}

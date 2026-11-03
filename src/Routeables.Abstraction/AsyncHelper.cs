using System.Globalization;

namespace Routeables.Abstraction;

/// <summary>
/// Helpers for the two places where a view model cannot simply <c>await</c>: a call it must not wait for (a load started from
/// <see cref="IRouteable.OnEnteringAsync"/>), and a call it must wait for from synchronous code.
/// </summary>
public static class AsyncHelper
{
    private static readonly TaskFactory Factory = new(
        CancellationToken.None, TaskCreationOptions.None, TaskContinuationOptions.None, TaskScheduler.Default);

    /// <summary>
    /// Starts <paramref name="work"/> and does not wait for it, without losing its exceptions: an unobserved fault of a discarded task
    /// (<c>_ = LoadAsync();</c>, <c>_ = Task.Run(...)</c>) is lost, or ends the process, depending on the runtime. Here an exception is
    /// reported to the <see cref="IRouteableErrorSink"/> (see <see cref="RouteableErrors"/>) and given to <paramref name="onError"/>; a
    /// cancellation is not an error.
    /// </summary>
    /// <remarks>
    /// <paramref name="work"/> is called on the calling thread and runs there until its first <c>await</c>, so what follows the
    /// <c>await</c> is back on the UI context (it can set a property bound to the view); use <c>Task.Run</c> inside it for work that
    /// must leave the UI thread.
    /// <code>
    /// public override Task OnEnteringAsync(IRouteableArgs args, CancellationToken ct = default)
    /// {
    ///     AsyncHelper.FireAndForget(async () =>
    ///     {
    ///         await cache.LoadAsync();
    ///         Persons = new(cache.Entities);
    ///     }, provider, onError: ex => Error = ex.Message);
    ///     return Task.CompletedTask;
    /// }
    /// </code>
    /// </remarks>
    /// <param name="work">What to run.</param>
    /// <param name="provider">Where to find the <see cref="IRouteableErrorSink"/>; <see langword="null"/> uses <see cref="RouteableErrors.Default"/>, or <see cref="System.Diagnostics.Trace"/>.</param>
    /// <param name="detail">What the work is about (shown after the source of the error), or <see langword="null"/>.</param>
    /// <param name="onError">Called with the exception, after it is reported (for instance to show it in the view); an exception thrown here is reported too.</param>
    public static void FireAndForget(Func<Task> work, IServiceProvider? provider = null, string? detail = null, Action<Exception>? onError = null)
    {
        ArgumentNullException.ThrowIfNull(work);

        Task task;
        try { task = work(); }
        catch (Exception ex) { Fail(ex, provider, detail, onError); return; }

        Forget(task, provider, detail, onError);
    }

    /// <summary>
    /// Does not wait for <paramref name="task"/>, and does not lose its exception: see <see cref="FireAndForget"/>.
    /// <c>LoadAsync().Forget(provider);</c>
    /// </summary>
    /// <param name="task">The task that runs on its own.</param>
    /// <param name="provider">Where to find the <see cref="IRouteableErrorSink"/>.</param>
    /// <param name="detail">What the task is about, or <see langword="null"/>.</param>
    /// <param name="onError">Called with the exception, after it is reported.</param>
    public static void Forget(this Task task, IServiceProvider? provider = null, string? detail = null, Action<Exception>? onError = null)
    {
        ArgumentNullException.ThrowIfNull(task);

        if (task.IsCompletedSuccessfully)
            return;

        _ = Observe(task, provider, detail, onError);
    }

    private static async Task Observe(Task task, IServiceProvider? provider, string? detail, Action<Exception>? onError)
    {
        try { await task.ConfigureAwait(true); }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Fail(ex, provider, detail, onError); }
    }

    private static void Fail(Exception exception, IServiceProvider? provider, string? detail, Action<Exception>? onError)
    {
        if (exception is OperationCanceledException)
            return;

        RouteableErrors.Report(provider, exception, typeof(AsyncHelper), detail, "FireAndForget");
        if (onError is null)
            return;

        try { onError(exception); }
        catch (Exception ex) { RouteableErrors.Report(provider, ex, typeof(AsyncHelper), detail, "FireAndForget.onError"); }
    }
}

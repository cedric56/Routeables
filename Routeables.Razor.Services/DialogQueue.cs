using Routeables.Abstraction;

namespace Routeables.Razor.Services;

/// <summary>A dialog waiting to be shown, or being shown: what a <see cref="DialogQueue{TRequest, TResult}"/> completes.</summary>
internal interface IDialogRequest<TResult>
{
    /// <summary>Completed with the result when the dialog is closed.</summary>
    TaskCompletionSource<TResult> Completion { get; }
}

/// <summary>
/// What the dialog services of this assembly share: the requests are queued and shown one at a time by a host placed once in
/// the application's layout, and a request that is still waiting when the circuit ends is completed rather than left hanging.
/// </summary>
internal abstract class DialogQueue<TRequest, TResult> : IDisposable
    where TRequest : class, IDialogRequest<TResult>
{
    private readonly Queue<TRequest> pending = new();
    private readonly object gate = new();

    /// <summary>Raised when <see cref="Current"/> changes. The host subscribes to it to re-render.</summary>
    public event Action? Changed;

    /// <summary>The dialog currently shown, or <see langword="null"/>.</summary>
    public TRequest? Current { get; private set; }

    /// <summary>The result a dialog still waiting gets when the circuit is over.</summary>
    protected abstract TResult ResultWhenDisposed(TRequest request);

    /// <summary>Shows the request now, or after the ones already waiting.</summary>
    /// <param name="host">The component that shows it (<c>RouteableDialogHost</c>), named in the error when it is missing.</param>
    /// <param name="kind">What it shows (<c>dialogs</c>, <c>task dialogs</c>), for the same error.</param>
    protected Task<TResult> EnqueueAsync(TRequest request, string host, string kind)
    {
        if (Changed is null)
            throw new InvalidOperationException(
                $"No dialog host is displayed. Add <{host} /> to your layout so {kind} can be shown.");

        lock (gate)
        {
            if (Current is null) Current = request;
            else pending.Enqueue(request);
        }

        Changed?.Invoke();
        return request.Completion.Task;
    }

    /// <summary>
    /// Completes <paramref name="request"/> with <paramref name="result"/> and shows the next one, unless it is no longer the
    /// current one (already closed). <paramref name="closed"/> runs first, and the dialog is completed even if it throws.
    /// </summary>
    protected void Finish(TRequest request, TResult result, Action? closed = null)
    {
        lock (gate)
        {
            if (!ReferenceEquals(Current, request)) return;
            Current = pending.Count > 0 ? pending.Dequeue() : null;
        }

        try
        {
            RouteableErrors.Guard(null, RouteableCallbackSources.DialogClosed, closed);
        }
        finally
        {
            request.Completion.TrySetResult(result);
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// The circuit is over: the dialogs still waiting are completed, or whoever awaits them (and everything those reference)
    /// would wait for ever, and the host stops being notified.
    /// </summary>
    public void Dispose()
    {
        List<TRequest> unfinished = [];
        lock (gate)
        {
            if (Current is not null) unfinished.Add(Current);
            unfinished.AddRange(pending);
            pending.Clear();
            Current = null;
        }

        Changed = null;
        foreach (var request in unfinished)
            request.Completion.TrySetResult(ResultWhenDisposed(request));
    }
}

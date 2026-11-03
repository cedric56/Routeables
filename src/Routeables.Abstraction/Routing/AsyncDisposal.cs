namespace Routeables.Abstraction;

/// <summary>Disposes, from a synchronous <c>Dispose</c>, an object that only offers <c>DisposeAsync</c>.</summary>
internal static class AsyncDisposal
{
    /// <summary>
    /// Starts <see cref="IAsyncDisposable.DisposeAsync"/> and does not wait for it. A synchronous <c>Dispose</c> cannot await, and blocking
    /// on the task is not an option on every host: it deadlocks a thread that owns a synchronization context (a UI thread), and it is not
    /// possible at all on Blazor WebAssembly, which has a single thread. The disposal then finishes after <c>Dispose</c> has returned
    /// (at once, when it completes synchronously); an error is reported to the error sink (<c>IRouteableErrorSink</c>), not thrown.
    /// Prefer <c>DisposeAsync</c> when the caller can await.
    /// </summary>
    public static void Start<TOwner>(IAsyncDisposable item)
    {
        ValueTask disposal;
        try
        {
            disposal = item.DisposeAsync();
        }
        catch (Exception ex)
        {
            RouteableErrors.Report<TOwner>(null, ex);
            return;
        }

        if (!disposal.IsCompletedSuccessfully)
            _ = Observe<TOwner>(disposal);
    }

    private static async Task Observe<TOwner>(ValueTask disposal)
    {
        try
        {
            await disposal.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            RouteableErrors.Report<TOwner>(null, ex);
        }
    }
}

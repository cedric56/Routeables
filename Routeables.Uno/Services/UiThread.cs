using Routeables.Uno;

namespace Routeables.Uno.Services;

internal static class UiThread
{
    /// <summary>Runs <paramref name="work"/> on the thread of the active window, whichever thread asked.</summary>
    public static Task<T> RunAsync<T>(Func<Task<T>> work)
    {
        var queue = RouteableWindows.ActiveRoot?.DispatcherQueue;
        if (queue is null || queue.HasThreadAccess)
            return work();

        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var queued = queue.TryEnqueue(async () =>
        {
            try { completion.SetResult(await work()); }
            catch (Exception ex) { completion.SetException(ex); }
        });

        if (!queued)
            completion.SetException(new InvalidOperationException("The window that should show the dialog is closed."));

        return completion.Task;
    }
}

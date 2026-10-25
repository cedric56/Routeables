using Routeables.Services;

namespace Routeables.Uno;

/// <summary>
/// The <see cref="IRouteableDispatcher"/> of Uno: the dispatcher queue of the active window. Before a window exists (or once the last
/// one is closed) there is no thread of the interface to go to, and the work runs on the calling thread.
/// </summary>
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public sealed class RouteableDispatcher : RouteableDispatcherBase
{
    /// <inheritdoc />
    public override bool CheckAccess() => RouteableWindows.ActiveRoot?.DispatcherQueue is not { } queue || queue.HasThreadAccess;

    /// <inheritdoc />
    public override void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var queue = RouteableWindows.ActiveRoot?.DispatcherQueue;
        if (queue is null)
        {
            action();
            return;
        }

        if (!queue.TryEnqueue(() => action()))
            throw new InvalidOperationException("The window that owns the interface is closed: the work cannot be queued.");
    }
}

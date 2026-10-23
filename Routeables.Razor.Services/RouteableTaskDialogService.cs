using Routeables.Abstraction;
using Routeables.Services;

namespace Routeables.Razor.Services;

/// <summary>A task dialog waiting to be shown, or being shown, by <see cref="RouteableTaskDialogHost"/>.</summary>
internal sealed class TaskDialogRequest : IDialogRequest<RouteableTaskDialogResult>
{
    public TaskDialogRequest(RouteableTaskDialog parameters, RouteableTaskDialogResult[] results, Action<TaskDialogRequest> finish)
    {
        Parameters = parameters;
        Session = new RouteableTaskDialogSession(parameters, results, () => finish(this));
    }

    public RouteableTaskDialog Parameters { get; }

    /// <summary>The buttons (one per result asked) and when the dialog may close, the same on every host.</summary>
    public RouteableTaskDialogSession Session { get; }

    public TaskCompletionSource<RouteableTaskDialogResult> Completion { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}

/// <summary>
/// Implements <see cref="IRouteableTaskDialogService"/> without any UI library. Dialogs are queued and shown one at a
/// time by a <see cref="RouteableTaskDialogHost"/> placed once in the application's layout.
/// </summary>
internal sealed class RouteableTaskDialogService : DialogQueue<TaskDialogRequest, RouteableTaskDialogResult>, IRouteableTaskDialogService
{
    protected override RouteableTaskDialogResult ResultWhenDisposed(TaskDialogRequest request) => RouteableTaskDialogResult.None;

    /// <inheritdoc />
    public Task<RouteableTaskDialogResult> ShowAsync(RouteableTaskDialog @params, params RouteableTaskDialogResult[] results)
    {
        ArgumentNullException.ThrowIfNull(@params);

        return EnqueueAsync(new TaskDialogRequest(@params, results, Close), "RouteableTaskDialogHost", "task dialogs");
    }

    /// <summary>Closes the current dialog as if no button had been pressed (Escape or a click on the backdrop), unless <see cref="RouteableTaskDialog.Closing"/> vetoes it.</summary>
    public void Dismiss()
    {
        if (Current is { } request && request.Session.TryDismiss())
            Close(request);
    }

    // A button decided the result, or the dialog was dismissed: it is closed and Closed is told.
    private void Close(TaskDialogRequest request) => Finish(request, request.Session.Result, request.Session.Release);
}

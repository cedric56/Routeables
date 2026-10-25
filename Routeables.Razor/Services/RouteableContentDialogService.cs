using Routeables.Abstraction;
using System.Windows.Input;
using Routeables.Services;

namespace Routeables.Razor.Services;

/// <summary>A content dialog waiting to be shown, or being shown, by <see cref="RouteableContentDialogHost"/>.</summary>
internal sealed class ContentDialogRequest : IDialogRequest<RouteableContentDialogResult>
{
    public ContentDialogRequest(RouteableContentDialog parameters, Action<ContentDialogRequest> finish)
    {
        Parameters = parameters;
        Session = new RouteableContentDialogSession(parameters, () => finish(this));
    }

    public RouteableContentDialog Parameters { get; }

    /// <summary>What the buttons do and when the dialog may close, the same on every host.</summary>
    public RouteableContentDialogSession Session { get; }

    public TaskCompletionSource<RouteableContentDialogResult> Completion { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}

/// <summary>
/// Implements <see cref="IRouteableContentDialogService"/> without any UI library. Dialogs are queued and shown one
/// at a time by a <see cref="RouteableContentDialogHost"/> placed once in the application's layout.
/// </summary>
internal sealed class RouteableContentDialogService : DialogQueue<ContentDialogRequest, RouteableContentDialogResult>, IRouteableContentDialogService
{
    protected override RouteableContentDialogResult ResultWhenDisposed(ContentDialogRequest request) => RouteableContentDialogResult.None;

    /// <inheritdoc />
    public Task<RouteableContentDialogResult> ShowAsync(RouteableContentDialog @params)
    {
        ArgumentNullException.ThrowIfNull(@params);

        var request = new ContentDialogRequest(@params, Close);

        // The queue is thread safe: the caller may close the dialog from any thread.
        request.Session.AllowCloseFromCode(close => close());
        return EnqueueAsync(request, "RouteableContentDialogHost", "content dialogs");
    }

    /// <summary>
    /// Closes the current dialog as if no button had been pressed (Escape or a click on the backdrop), unless
    /// <see cref="RouteableContentDialog.Closing"/> vetoes it.
    /// </summary>
    public void Dismiss()
    {
        if (Current is { } request && request.Session.TryDismiss())
            Close(request);
    }

    // A button decided the result, or the dialog was dismissed: it is closed and Closed is told.
    private void Close(ContentDialogRequest request) => Finish(request, request.Session.Result, request.Session.Release);
}

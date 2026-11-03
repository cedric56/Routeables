using Routeables.Services;

namespace Routeables.Razor.Services;

/// <summary>One button of a dialog and the value it completes the dialog with.</summary>
internal sealed record DialogButton(string Text, int Result, bool IsPrimary = false);

/// <summary>A dialog waiting to be shown, or being shown, by <see cref="RouteableDialogHost"/>.</summary>
internal sealed class DialogRequest(string title, string message, DialogButton[] buttons, int dismissResult) : IDialogRequest<int>
{
    public string Title { get; } = title;

    public string Message { get; } = message;

    public DialogButton[] Buttons { get; } = buttons;

    /// <summary>The result when the user dismisses the dialog with Escape or by clicking the backdrop.</summary>
    public int DismissResult { get; } = dismissResult;

    public TaskCompletionSource<int> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
}

/// <summary>
/// The message boxes waiting to be shown. Requests are queued and shown one at a time by a <see cref="RouteableDialogHost"/>
/// placed once in the application's layout.
/// </summary>
internal sealed class RouteableMessageQueue : DialogQueue<DialogRequest, int>
{
    protected override int ResultWhenDisposed(DialogRequest request) => request.DismissResult;

    /// <summary>Completes the current dialog with <paramref name="result"/> and shows the next one, if any.</summary>
    /// <param name="result">The <see cref="DialogButton.Result"/> of the button that was clicked.</param>
    public void Complete(int result)
    {
        if (Current is { } request)
            Finish(request, result);
    }

    /// <summary>Shows a message box and returns the index of the button that closed it.</summary>
    /// <param name="title">The title.</param>
    /// <param name="message">The message.</param>
    /// <param name="dismissResult">The result when the dialog is dismissed without a button.</param>
    /// <param name="buttons">The buttons, in the order they are drawn.</param>
    public Task<int> ShowAsync(string title, string message, int dismissResult, params DialogButton[] buttons)
        => EnqueueAsync(new DialogRequest(title, message, buttons, dismissResult), "RouteableDialogHost", "dialogs");
}

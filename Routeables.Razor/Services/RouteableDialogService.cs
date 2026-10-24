using Routeables.Abstraction;
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
/// Implements <see cref="IRouteableDialogService"/> without any UI library. Requests are queued and shown one at a
/// time by a <see cref="RouteableDialogHost"/> placed once in the application's layout.
/// </summary>
internal sealed class RouteableDialogService : DialogQueue<DialogRequest, int>, IRouteableDialogService
{
    private const int First = 0;
    private const int Second = 1;
    private const int Third = 2;

    protected override int ResultWhenDisposed(DialogRequest request) => request.DismissResult;

    /// <summary>Completes the current dialog with <paramref name="result"/> and shows the next one, if any.</summary>
    public void Complete(int result)
    {
        if (Current is { } request)
            Finish(request, result);
    }

    private Task<int> ShowAsync(string title, string message, int dismissResult, params DialogButton[] buttons)
    {
        return EnqueueAsync(new DialogRequest(title, message, buttons, dismissResult), "RouteableDialogHost", "dialogs");
    }

    /// <inheritdoc />
    public async Task ShowErrorAsync(Exception ex, string title = "Error")
    {
        ArgumentNullException.ThrowIfNull(ex);
        await ShowAsync(title, ex.Message, First, new DialogButton("OK", First, IsPrimary: true));
    }

    /// <inheritdoc />
    public async Task ShowOkAsync(string message, string title = "Title")
        => await ShowAsync(title, message, First, new DialogButton("OK", First, IsPrimary: true));

    /// <inheritdoc />
    public async Task<bool> ShowOkCancelAsync(string message, string title = "Title")
        => await ShowAsync(title, message, Second,
            new DialogButton("Cancel", Second), new DialogButton("OK", First, IsPrimary: true)) == First;

    /// <inheritdoc />
    public async Task<bool> ShowOkAbortAsync(string message, string title = "Title")
        => await ShowAsync(title, message, Second,
            new DialogButton("Abort", Second), new DialogButton("OK", First, IsPrimary: true)) == First;

    /// <inheritdoc />
    public async Task<bool> ShowYesNoAsync(string message, string title = "Title")
        => await ShowAsync(title, message, Second,
            new DialogButton("No", Second), new DialogButton("Yes", First, IsPrimary: true)) == First;

    /// <inheritdoc />
    public Task<int> ShowYesNoCancelAsync(string message, string title = "Title")
        => ShowAsync(title, message, Third,
            new DialogButton("Cancel", Third), new DialogButton("No", Second), new DialogButton("Yes", First, IsPrimary: true));

    /// <inheritdoc />
    public Task<int> ShowYesNoAbortAsync(string message, string title = "Title")
        => ShowAsync(title, message, Third,
            new DialogButton("Abort", Third), new DialogButton("No", Second), new DialogButton("Yes", First, IsPrimary: true));
}
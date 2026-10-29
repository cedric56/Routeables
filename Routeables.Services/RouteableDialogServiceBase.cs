namespace Routeables.Services;

/// <summary>
/// The base of the dialog services of the hosts that draw one message box with the buttons they are given. A derived
/// service only says how to show it, in <see cref="ShowMessageAsync"/>: the confirmations are written once, here.
/// </summary>
public abstract class RouteableDialogServiceBase : IRouteableDialogService
{
    /// <summary>Shows a message with buttons and returns the index of the one chosen, or -1 when the dialog was dismissed.</summary>
    /// <param name="message">The message.</param>
    /// <param name="title">The title.</param>
    /// <param name="buttonText">The buttons written together, one word per capital letter: <c>"YesNoCancel"</c> is Yes, No and Cancel. See <see cref="RouteableButtons.Split"/>.</param>
    public abstract Task<int> ShowMessageAsync(string message, string title, string buttonText);

    /// <inheritdoc />
    public Task ShowErrorAsync(Exception ex, string title = "Error")
    {
        ArgumentNullException.ThrowIfNull(ex);
        return ShowMessageAsync(ex.Message, title, "Ok");
    }

    /// <inheritdoc />
    public Task ShowOkAsync(string message, string title = "Title")
        => ShowMessageAsync(message, title, "Ok");

    /// <inheritdoc />
    public async Task<bool> ShowYesNoAsync(string message, string title = "Title")
        => await ShowMessageAsync(message, title, "YesNo").ConfigureAwait(false) == 0;

    /// <inheritdoc />
    public async Task<bool> ShowOkCancelAsync(string message, string title = "Title")
        => await ShowMessageAsync(message, title, "OkCancel").ConfigureAwait(false) == 0;

    /// <inheritdoc />
    public async Task<bool> ShowOkAbortAsync(string message, string title = "Title")
        => await ShowMessageAsync(message, title, "OkAbort").ConfigureAwait(false) == 0;

    /// <inheritdoc />
    public Task<int> ShowYesNoCancelAsync(string message, string title = "Title")
        => ShowMessageAsync(message, title, "YesNoCancel");

    /// <inheritdoc />
    public Task<int> ShowYesNoAbortAsync(string message, string title = "Title")
        => ShowMessageAsync(message, title, "YesNoAbort");
    /// <inheritdoc />
    public abstract Task<RouteableContentDialogResult> ShowContentDialogAsync(RouteableContentDialog @params);
    /// <inheritdoc />
    public abstract Task<RouteableTaskDialogResult> ShowTaskDialogAsync(RouteableTaskDialog @params, params RouteableTaskDialogResult[] results);
}

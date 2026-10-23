namespace Routeables.Services;

/// <summary>
/// The base of the content dialog services of the hosts that draw a content dialog themselves. A derived service only says
/// how to show it, in <see cref="ShowAsync"/>: it then answers the simple message boxes
/// (<see cref="IRouteableDialogService"/>) with a content dialog, so the confirmations are written once, here.
/// </summary>
public abstract class RouteableContentDialogServiceBase : IRouteableContentDialogService, IRouteableDialogService
{
    /// <inheritdoc />
    public abstract Task<RouteableContentDialogResult> ShowAsync(RouteableContentDialog @params);

    /// <inheritdoc />
    public Task ShowErrorAsync(Exception ex, string title = "Error")
    {
        ArgumentNullException.ThrowIfNull(ex);
        return AskAsync(ex.Message, title, close: "Ok");
    }

    /// <inheritdoc />
    public Task ShowOkAsync(string message, string title = "Title")
        => AskAsync(message, title, close: "Ok");

    /// <inheritdoc />
    public async Task<bool> ShowYesNoAsync(string message, string title = "Title")
        => await AskAsync(message, title, close: "No", primary: "Yes").ConfigureAwait(false) == RouteableContentDialogResult.Primary;

    /// <inheritdoc />
    public async Task<bool> ShowOkCancelAsync(string message, string title = "Title")
        => await AskAsync(message, title, close: "Cancel", primary: "Ok").ConfigureAwait(false) == RouteableContentDialogResult.Primary;

    /// <inheritdoc />
    public async Task<bool> ShowOkAbortAsync(string message, string title = "Title")
        => await AskAsync(message, title, close: "Abort", primary: "Ok").ConfigureAwait(false) == RouteableContentDialogResult.Primary;

    /// <inheritdoc />
    public Task<int> ShowYesNoCancelAsync(string message, string title = "Title")
        => AskYesNoAsync(message, title, close: "Cancel");

    /// <inheritdoc />
    public Task<int> ShowYesNoAbortAsync(string message, string title = "Title")
        => AskYesNoAsync(message, title, close: "Abort");

    // Yes, No and a third way out (Cancel or Abort): 0, 1, 2.
    private async Task<int> AskYesNoAsync(string message, string title, string close)
    {
        var result = await AskAsync(message, title, close, primary: "Yes", secondary: "No").ConfigureAwait(false);
        return result switch
        {
            RouteableContentDialogResult.Primary => 0,
            RouteableContentDialogResult.Secondary => 1,
            _ => 2
        };
    }

    // A message with the buttons it is given; a button without a text is not shown.
    private Task<RouteableContentDialogResult> AskAsync(string message, string title, string close, string? primary = null, string? secondary = null)
        => ShowAsync(new RouteableContentDialog()
        {
            CloseButtonText = close,
            PrimaryButtonText = primary,
            SecondaryButtonText = secondary,
            Title = title,
            Content = message
        });
}

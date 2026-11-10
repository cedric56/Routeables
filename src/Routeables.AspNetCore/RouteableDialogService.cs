using Routeables.Abstraction.Errors;
using Routeables.Services;

namespace Routeables.AspNetCore;

/// <summary>
/// The dialogs of the web host: a dialog is put on the page of the visitor (see <see cref="RouteableInteractions"/>) and the view model awaits
/// the button that is chosen, like it awaits a window on a desktop.
/// </summary>
public sealed class RouteableDialogService(RouteableInteractions interactions) : RouteableDialogServiceBase
{
    /// <summary>
    /// Shows a simple message dialog with a single button and returns the chosen button index (always 0).
    /// </summary>
    public override Task<int> ShowMessageAsync(string message, string title, string buttonText)
        => interactions.AskAsync(new RouteableDialog(title, null, message, RouteableButtons.Split(buttonText)));

    /// <summary>
    /// Shows a content dialog (primary / secondary / close) and returns which button the visitor selected.
    /// </summary>
    public override async Task<RouteableContentDialogResult> ShowContentDialogAsync(RouteableContentDialog @params)
    {
        // The buttons that have a text, in the order of the other hosts: primary, secondary, close.
        var choices = new List<(string Text, RouteableContentDialogResult Result, Action? Click)>();
        if (!string.IsNullOrEmpty(@params.PrimaryButtonText) && @params.IsPrimaryButtonEnabled)
            choices.Add((@params.PrimaryButtonText, RouteableContentDialogResult.Primary, @params.PrimaryButtonClick));
        if (!string.IsNullOrEmpty(@params.SecondaryButtonText) && @params.IsSecondaryButtonEnabled)
            choices.Add((@params.SecondaryButtonText, RouteableContentDialogResult.Secondary, @params.SecondaryButtonClick));
        choices.Add((string.IsNullOrEmpty(@params.CloseButtonText) ? "Close" : @params.CloseButtonText, RouteableContentDialogResult.None, @params.CloseButtonClick));

        RouteableErrors.Guard(null, RouteableCallbackSources.DialogOpening, @params.Opening);
        var dialog = new RouteableDialog(@params.Title ?? string.Empty, null, @params.Content?.ToString(), [.. choices.Select(c => c.Text)]);
        var answer = interactions.AskAsync(dialog);
        RouteableErrors.Guard(null, RouteableCallbackSources.DialogOpened, @params.Opened);

        var chosen = choices[await answer];
        RouteableErrors.Guard(null, RouteableCallbackSources.ContentDialogClick, chosen.Click);
        RouteableErrors.Guard(null, RouteableCallbackSources.DialogClosed, @params.Closed);
        return chosen.Result;
    }

    /// <summary>
    /// Shows a task dialog with the supplied result buttons (or OK by default) and returns the chosen result.
    /// </summary>
    public override async Task<RouteableTaskDialogResult> ShowTaskDialogAsync(RouteableTaskDialog @params, params RouteableTaskDialogResult[] results)
    {
        RouteableTaskDialogResult[] buttons = results.Length > 0 ? results : [RouteableTaskDialogResult.OK];

        RouteableErrors.Guard(null, RouteableCallbackSources.DialogOpening, @params.Opening);
        var dialog = new RouteableDialog(@params.Title ?? string.Empty, @params.Header, @params.Content?.ToString(), [.. buttons.Select(b => b.ToString())])
        {
            Footer = @params.ShowsFooter ? @params.Footer?.ToString() : null,
            ShowProgress = @params.ShowProgressBar
        };
        var answer = interactions.AskAsync(dialog);
        RouteableErrors.Guard(null, RouteableCallbackSources.DialogOpened, @params.Opened);

        var result = buttons[await answer];
        RouteableErrors.Guard(null, RouteableCallbackSources.DialogClosed, @params.Closed);
        return result;
    }
}
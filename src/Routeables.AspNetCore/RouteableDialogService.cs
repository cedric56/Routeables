using Routeables.Services;

namespace Routeables.AspNetCore;

/// <summary>
/// The dialogs of the web host: a dialog is put on the page of the visitor (see <see cref="WebInteractions"/>) and the view model awaits
/// the button that is chosen, like it awaits a window on a desktop.
/// </summary>
public sealed class RouteableDialogService(RouteableInteractions interactions) : RouteableDialogServiceBase
{
    public override Task<int> ShowMessageAsync(string message, string title, string buttonText)
        => interactions.AskAsync(new RouteableDialog(title, null, message, RouteableButtons.Split(buttonText)));

    public override async Task<RouteableContentDialogResult> ShowContentDialogAsync(RouteableContentDialog @params)
    {
        // The buttons that have a text, in the order of the other hosts: primary, secondary, close.
        var choices = new List<(string Text, RouteableContentDialogResult Result, Action? Click)>();
        if (!string.IsNullOrEmpty(@params.PrimaryButtonText) && @params.IsPrimaryButtonEnabled)
            choices.Add((@params.PrimaryButtonText, RouteableContentDialogResult.Primary, @params.PrimaryButtonClick));
        if (!string.IsNullOrEmpty(@params.SecondaryButtonText) && @params.IsSecondaryButtonEnabled)
            choices.Add((@params.SecondaryButtonText, RouteableContentDialogResult.Secondary, @params.SecondaryButtonClick));
        choices.Add((string.IsNullOrEmpty(@params.CloseButtonText) ? "Close" : @params.CloseButtonText, RouteableContentDialogResult.None, @params.CloseButtonClick));

        @params.Opening?.Invoke();
        var dialog = new RouteableDialog(@params.Title ?? string.Empty, null, @params.Content?.ToString(), [.. choices.Select(c => c.Text)]);
        var answer = interactions.AskAsync(dialog);
        @params.Opened?.Invoke();

        var chosen = choices[await answer];
        chosen.Click?.Invoke();
        @params.Closed?.Invoke();
        return chosen.Result;
    }

    public override async Task<RouteableTaskDialogResult> ShowTaskDialogAsync(RouteableTaskDialog @params, params RouteableTaskDialogResult[] results)
    {
        RouteableTaskDialogResult[] buttons = results.Length > 0 ? results : [RouteableTaskDialogResult.OK];

        @params.Opening?.Invoke();
        var dialog = new RouteableDialog(@params.Title ?? string.Empty, @params.Header, @params.Content?.ToString(), [.. buttons.Select(b => b.ToString())])
        {
            Footer = @params.ShowsFooter ? @params.Footer?.ToString() : null,
            ShowProgress = @params.ShowProgressBar
        };
        var answer = interactions.AskAsync(dialog);
        @params.Opened?.Invoke();

        var result = buttons[await answer];
        @params.Closed?.Invoke();
        return result;
    }
}

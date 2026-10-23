using Avalonia.Controls;
using Avalonia.Media;
using FluentAvalonia.Core;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Windowing;
using Routeables.Abstraction;
using Routeables.Avalonia;
using Routeables.Avalonia.Services;
using Routeables.Services;

namespace Reshapes.Avalonia.Fluent;

public class ReshapeMessageBoxService : IMessageBoxService
{
    public bool IsDisplayable(TopLevel topLevel)
    {
        return topLevel is not FAAppWindow;
    }

    public async Task<RouteableContentDialogResult> ShowAsync(RouteableContentDialog @params, TopLevel? topLevel = null)
    {
        var dialog = GetContentDialog(@params);
        var result = await dialog.ShowAsync(topLevel);
        return Enum.TryParse(result.ToString(), out RouteableContentDialogResult dialogResult)
           ? dialogResult
           : RouteableContentDialogResult.None;

        FAContentDialog GetContentDialog(RouteableContentDialog @params)
        {
            var dialog = new FAContentDialog();
            if (@params != null)
            {
                TypedEventHandler<FAContentDialog, FAContentDialogClosingEventArgs>? closing = null;
                TypedEventHandler<FAContentDialog, EventArgs>? opening = null;
                TypedEventHandler<FAContentDialog, EventArgs>? opened = null;
                TypedEventHandler<FAContentDialog, FAContentDialogClosedEventArgs>? closed = null;
                TypedEventHandler<FAContentDialog, FAContentDialogButtonClickEventArgs>? primaryButtonClick = null;
                TypedEventHandler<FAContentDialog, FAContentDialogButtonClickEventArgs>? secondaryButtonClick = null;
                TypedEventHandler<FAContentDialog, FAContentDialogButtonClickEventArgs>? closeButtonClick = null;

                dialog.Content = @params.Content;
                dialog.PrimaryButtonText = @params.PrimaryButtonText;
                dialog.SecondaryButtonText = @params.SecondaryButtonText;
                dialog.CloseButtonText = @params.CloseButtonText;
                dialog.Title = @params.Title;
                dialog.CloseButtonCommand = @params.CloseButtonCommand;
                dialog.CloseButtonCommandParameter = @params.CloseButtonCommandParameter;
                dialog.FullSizeDesired = @params.FullSizeDesired;
                dialog.PrimaryButtonCommand = @params.PrimaryButtonCommand;
                dialog.PrimaryButtonCommandParameter = @params.PrimaryButtonCommandParameter;
                dialog.SecondaryButtonCommand = @params.SecondaryButtonCommand;
                dialog.SecondaryButtonCommandParameter = @params.SecondaryButtonCommandParameter;
                dialog.Opening += opening = (sender, args) => RouteableErrors.Guard(null, "Dialog.Opening", @params.Opening);
                dialog.Closing += closing = (sender, args) => args.Cancel = RouteableErrors.Guard(null, "ContentDialog.Closing", @params.Closing, args.Result.ToString());
                dialog.PrimaryButtonClick += primaryButtonClick = (sender, args) => RouteableErrors.Guard(null, "ContentDialog.PrimaryButtonClick", @params.PrimaryButtonClick);
                dialog.SecondaryButtonClick += secondaryButtonClick = (sender, args) => RouteableErrors.Guard(null, "ContentDialog.SecondaryButtonClick", @params.SecondaryButtonClick);
                dialog.CloseButtonClick += closeButtonClick = (sender, args) => RouteableErrors.Guard(null, "ContentDialog.CloseButtonClick", @params.CloseButtonClick);
                dialog.Opened += opened = (sender, args) =>
                {
                    RouteableErrors.Guard(null, "Dialog.Opened", @params.Opened);
                };
                dialog.Closed += closed = (sender, args) =>
                {
                    RouteableErrors.Guard(null, "Dialog.Closed", @params.Closed);
                    dialog.Opened -= opened;
                    dialog.Opening -= opening;
                    dialog.Closing -= closing;
                    dialog.PrimaryButtonClick -= primaryButtonClick;
                    dialog.SecondaryButtonClick -= secondaryButtonClick;
                    dialog.CloseButtonClick -= closeButtonClick;
                    dialog.Closed -= closed;
                };
            }

            return dialog;
        }
    }

    public async Task<RouteableTaskDialogResult> ShowAsync(RouteableTaskDialog @params, TopLevel? topLevel = null, params RouteableTaskDialogResult[] results)
    {
        TypedEventHandler<FATaskDialog, EventArgs>? opening = null;
        TypedEventHandler<FATaskDialog, EventArgs>? opened = null;
        TypedEventHandler<FATaskDialog, FATaskDialogClosingEventArgs>? closing = null;
        TypedEventHandler<FATaskDialog, EventArgs>? closed = null;

        var taskDialog = new FATaskDialog()
        {
            Buttons = CreateDialogButtons(results),
            Content = @params.Content,
            Title = @params.Title,
            Header = @params.Header,
            SubHeader = @params.SubHeader,
            IconSource = @params.IconSource as FAIconSource,
            ShowProgressBar = @params.ShowProgressBar,
            FooterVisibility = Enum.Parse<FATaskDialogFooterVisibility>(@params.FooterVisibility.ToString()),
            IsFooterExpanded = @params.IsFooterExpanded,
            Footer = @params.Footer,
            XamlRoot = topLevel
        };

        taskDialog.Opening += opening = (sender, args) => RouteableErrors.Guard(null, "Dialog.Opening", @params.Opening);
        taskDialog.Opened += opened = (sender, args) => RouteableErrors.Guard(null, "Dialog.Opened", @params.Opened);
        taskDialog.Closing += closing = (sender, args) => args.Cancel = RouteableErrors.Guard(null, "TaskDialog.Closing", @params.Closing);
        taskDialog.Closed += closed = (sender, args) =>
        {
            RouteableErrors.Guard(null, "Dialog.Closed", @params.Closed);

            taskDialog.Opened -= opened;
            taskDialog.Opening -= opening;
            taskDialog.Closing -= closing;
            taskDialog.Closed -= closed;
        };
        var result = await taskDialog.ShowAsync();
        return (RouteableTaskDialogResult)result;

        List<FATaskDialogButton> CreateDialogButtons(RouteableTaskDialogResult[] results)
        {
            var buttons = new List<FATaskDialogButton>();
            foreach (var result in results)
            {
                FATaskDialogButton? button = result switch
                {
                    RouteableTaskDialogResult.OK => FATaskDialogButton.OKButton,
                    RouteableTaskDialogResult.Retry => FATaskDialogButton.RetryButton,
                    RouteableTaskDialogResult.Yes => FATaskDialogButton.YesButton,
                    RouteableTaskDialogResult.No => FATaskDialogButton.NoButton,
                    RouteableTaskDialogResult.Cancel => FATaskDialogButton.CancelButton,
                    RouteableTaskDialogResult.Close => FATaskDialogButton.CloseButton,
                    _ => null
                };
                if (button != null)
                {
                    buttons.Add(button);
                }
            }
            return buttons;
        }
    }

    /// <summary>
    /// Shows a message box as a content dialog. <paramref name="buttons"/> lists the button names written in
    /// PascalCase ("Ok", "OkCancel", "YesNo", "YesNoCancel", "YesNoAbort"…); a content dialog has at most three
    /// buttons: the first is the primary button, the last one the close button (so Escape and the window's close
    /// button answer with the last name, usually Cancel/No/Abort), and a middle one the secondary button.
    /// </summary>
    /// <returns>The name of the button that was pressed.</returns>
    public async Task<string?> ShowMessageBoxAsync(string title, string message, string buttons, TopLevel? topLevel = null)
    {
        var labels = (buttons ?? string.Empty).SplitOnCapitals().ToList();
        if (labels.Count == 0)
            labels.Add("Ok");

        if (labels.Count > 3)
            throw new ArgumentException($"A content dialog has at most three buttons, but '{buttons}' defines {labels.Count}.", nameof(buttons));

        var dialog = new RouteableContentDialog
        {
            Title = title,
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap }
        };

        if (labels.Count == 1)
        {
            dialog.CloseButtonText = labels[0];
        }
        else
        {
            dialog.PrimaryButtonText = labels[0];
            dialog.IsPrimaryButtonEnabled = true;
            dialog.CloseButtonText = labels[^1];

            if (labels.Count == 3)
            {
                dialog.SecondaryButtonText = labels[1];
                dialog.IsSecondaryButtonEnabled = true;
            }
        }

        var result = await ShowAsync(dialog, topLevel);

        return result switch
        {
            RouteableContentDialogResult.Primary when labels.Count > 1 => labels[0],
            RouteableContentDialogResult.Secondary when labels.Count == 3 => labels[1],
            // The close button, Escape and the window's close button all end up here.
            _ => labels[^1]
        };
    }
}
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Reshapes.Razor.MudBlazor.Components;
using Routeables.Abstraction;
using Routeables.Razor.Services;
using Routeables.Services;

namespace Reshapes.Razor.MudBlazor
{
    internal class ReshapeMessageBoxService(IDialogService MudDialogService) : IMessageBoxService
    {
        /// <summary>
        /// HTML-encodes a message string and replaces line breaks with HTML line break tags (<c>&lt;br /&gt;</c>) 
        /// to ensure proper rendering inside message boxes.
        /// </summary>
        /// <param name="message">The raw message string.</param>
        /// <returns>A sanitized <see cref="MarkupString"/> safe for display.</returns>
        private MarkupString SanitizeMessage(string message)
        {
            return (MarkupString)System.Text.RegularExpressions.Regex.Replace(System.Web.HttpUtility.HtmlEncode(message), "\r?\n|\r", "<br />");
        }

        /// <summary>
        /// Displays a content dialog asynchronously with the specified parameters.
        /// </summary>
        /// <param name="params">The parameters governing the content dialog's configuration, content, and events.</param>
        /// <returns>
        /// A task that completes with the <see cref="RouteableContentDialogResult"/> indicating how the user closed the dialog.
        /// </returns>
        public async Task<RouteableContentDialogResult> ShowContentDialogAsync(RouteableContentDialog @params)
        {
            var dialog = await MudDialogService.ShowAsync<ContentDialog>(@params.Title,
            new DialogParameters() { { nameof(ContentDialog.Parameters), @params } },
            new DialogOptions() { BackdropClick = true });

            var result = await dialog.Result;

            return result?.Data is RouteableContentDialogResult cdr ? cdr : RouteableContentDialogResult.None;
        }

        /// <summary>
        /// Shows a message box whose buttons are the words of <paramref name="buttons"/> ("Ok", "YesNo", "YesNoCancel"...), at most three: MudBlazor
        /// draws them as its Yes, No and Cancel buttons.
        /// </summary>
        /// <returns>The index of the button chosen; the last one when the dialog was dismissed.</returns>
        public async Task<int> ShowMessageAsync(string title, string message, string buttons)
        {
            var words = Words(buttons);
            var result = await MudDialogService.ShowMessageBoxAsync(
                title,
                SanitizeMessage(message),
                yesText: words[0],
                noText: words.Count > 1 ? words[1] : null,
                cancelText: words.Count > 2 ? words[2] : null,
                options: new DialogOptions
                {
                    BackdropClick = true,
                    CloseOnEscapeKey = true
                });
            return result switch
            {
                true => 0,
                false => 1,
                _ => words.Count - 1
            };
        }

        // "YesNoCancel" is Yes, No, Cancel: one word for each capital. Nothing is "OK" but "Ok".
        private static IReadOnlyList<string> Words(string? buttons)
        {
            var words = string.IsNullOrEmpty(buttons)
                ? []
                : System.Text.RegularExpressions.Regex.Matches(buttons, @"\p{Lu}\p{Ll}*").Select(m => m.Value == "Ok" ? "OK" : m.Value).Take(3).ToList();
            return words.Count == 0 ? ["OK"] : words;
        }

        /// <summary>A modal is shown by its own component, <c>RouteableModalFor&lt;TView, TViewModel, TResult&gt;.ShowModalAsync()</c>.</summary>
        /// <exception cref="NotSupportedException">Always: a view model alone does not say which component draws it.</exception>
        public Task<TResult?> ShowModalAsync<TViewModel, TResult>(TViewModel viewModel) where TViewModel : ICloseableViewModel<TResult>
            => throw new NotSupportedException(
                "A modal needs its component: derive it from RouteableModalFor<TView, TViewModel, TResult> and call ShowModalAsync() on it.");

        /// <summary>
        /// Displays a task dialog asynchronously with the specified parameters and available result choices.
        /// </summary>
        /// <param name="params">The parameters governing the task dialog's configuration, text, icon, and content.</param>
        /// <param name="results">An array of standard results representing the action buttons to present to the user.</param>
        /// <returns>
        /// A task that completes with the <see cref="RouteableTaskDialogResult"/> corresponding to the button selected by the user.
        /// </returns>
        public async Task<RouteableTaskDialogResult> ShowTaskDialogAsync(RouteableTaskDialog @params, params RouteableTaskDialogResult[] results)
        {
            var dialog = await MudDialogService.ShowAsync<TaskDialog>(@params.Title,
            new DialogParameters()
            {
                    { nameof(TaskDialog.Parameters), @params },
                    { nameof(TaskDialog.Results), results }
            },
            new DialogOptions() { BackdropClick = true });

            var result = await dialog.Result;
            return result?.Data is RouteableTaskDialogResult cdr ? cdr : RouteableTaskDialogResult.None;
        }
    }
}

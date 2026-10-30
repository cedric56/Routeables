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

        public async Task<int> ShowMessageAsync(string title, string message, string buttons)
        {
            var result = await MudDialogService.ShowMessageBoxAsync(
                title,
                 SanitizeMessage(message),
                yesText: "Yes",
                noText: "No",
                cancelText: "Abort",
                options: new DialogOptions
                {
                    BackdropClick = true,
                    CloseOnEscapeKey = true
                });
            return result switch
            {
                true => 0,
                false => 1,
                _ => 2
            };
        }

        public Task<TResult?> ShowModalAsync<TViewModel, TResult>(TViewModel viewModel) where TViewModel : ICloseableViewModel<TResult>
        {
            throw new NotImplementedException();
        }

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

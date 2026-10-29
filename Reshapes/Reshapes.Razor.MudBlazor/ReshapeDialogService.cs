//using Microsoft.AspNetCore.Components;
//using MudBlazor;
//using Routeables.Abstraction;
//using Routeables.Services;

//namespace Reshapes.Razor.MudBlazor;

///// <summary>
///// Provides a concrete implementation of <see cref="IRouteableDialogService"/> using MudBlazor's dialog service 
///// to display standard message boxes, error dialogs, and confirmation prompts asynchronously.
///// </summary>
///// <param name="MudDialogService">The underlying MudBlazor dialog service.</param>
//internal class ReshapeDialogService(IDialogService MudDialogService) : IRouteableDialogService
//{
//    /// <summary>
//    /// HTML-encodes a message string and replaces line breaks with HTML line break tags (<c>&lt;br /&gt;</c>) 
//    /// to ensure proper rendering inside message boxes.
//    /// </summary>
//    /// <param name="message">The raw message string.</param>
//    /// <returns>A sanitized <see cref="MarkupString"/> safe for display.</returns>
//    private MarkupString SanitizeMessage(string message)
//    {
//        return (MarkupString)System.Text.RegularExpressions.Regex.Replace(System.Web.HttpUtility.HtmlEncode(message), "\r?\n|\r", "<br />");
//    }

//    /// <inheritdoc />
//    public async Task ShowErrorAsync(Exception ex, string title = "Error")
//    {
//        await MudDialogService.ShowMessageBoxAsync(
//            title,
//            SanitizeMessage(ex.Message),
//            options: new DialogOptions
//            {
//                BackdropClick = true,
//                CloseOnEscapeKey = true
//            });
//    }

//    /// <inheritdoc />
//    public async Task<bool> ShowOkAbortAsync(string message, string title = "Title")
//    {
//        return await MudDialogService.ShowMessageBoxAsync(
//            title,
//            SanitizeMessage(message),
//            cancelText: "Abort",
//            options: new DialogOptions
//            {
//                BackdropClick = true,
//                CloseOnEscapeKey = true
//            }) ?? false;
//    }

//    /// <inheritdoc />
//    public async Task ShowOkAsync(string message, string title = "Title")
//    {
//        await MudDialogService.ShowMessageBoxAsync(
//            title,
//            SanitizeMessage(message),
//            options: new DialogOptions
//            {
//                BackdropClick = true,
//                CloseOnEscapeKey = true
//            });
//    }

//    /// <inheritdoc />
//    public async Task<bool> ShowOkCancelAsync(string message, string title = "Title")
//    {
//        return await MudDialogService.ShowMessageBoxAsync(
//            title,
//             SanitizeMessage(message),
//            cancelText: "Cancel",
//            options: new DialogOptions
//            {
//                BackdropClick = true,
//                CloseOnEscapeKey = true
//            }) ?? false;
//    }

//    /// <inheritdoc />
//    public async Task<int> ShowYesNoAbortAsync(string message, string title = "Title")
//    {
//        var result = await MudDialogService.ShowMessageBoxAsync(
//            title,
//             SanitizeMessage(message),
//            yesText: "Yes",
//            noText: "No",
//            cancelText: "Abort",
//            options: new DialogOptions
//            {
//                BackdropClick = true,
//                CloseOnEscapeKey = true
//            });
//        return result switch
//        {
//            true => 0,
//            false => 1,
//            _ => 2
//        };
//    }

//    /// <inheritdoc />
//    public async Task<bool> ShowYesNoAsync(string message, string title = "Title")
//    {
//        return await MudDialogService.ShowMessageBoxAsync(
//            title,
//             SanitizeMessage(message),
//            yesText: "Yes",
//            cancelText: "No",
//            options: new DialogOptions
//            {
//                BackdropClick = true,
//                CloseOnEscapeKey = true
//            }) ?? false;
//    }

//    /// <inheritdoc />
//    public async Task<int> ShowYesNoCancelAsync(string message, string title = "Title")
//    {
//        var result = await MudDialogService.ShowMessageBoxAsync(
//            title,
//             SanitizeMessage(message),
//            yesText: "Yes",
//            noText: "No",
//            cancelText: "Cancel",
//            options: new DialogOptions
//            {
//                BackdropClick = true,
//                CloseOnEscapeKey = true
//            });
//        return result switch
//        {
//            true => 0,
//            false => 1,
//            _ => 2
//        };
//    }
//}
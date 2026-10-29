using Avalonia.Controls;
using Routeables.Services;

namespace Routeables.Avalonia.Services;

/// <summary>
/// Where the dialogs of the Avalonia host are drawn. <c>DefaultMessageBoxService</c> draws them itself; a Reshape (such as
/// <c>ReshapeMessageBoxService</c> for FluentAvalonia) replaces it to draw them with a UI library. Pass yours to
/// <c>AppBuilder.UseRouteables(messageBoxService: ...)</c>.
/// </summary>
public interface IMessageBoxService
{
    /// <summary>Shows a message with buttons and waits for the user to choose one.</summary>
    /// <param name="title">The title of the dialog.</param>
    /// <param name="message">The text of the dialog.</param>
    /// <param name="buttons">The labels of the buttons, written together in PascalCase (<c>"YesNo"</c> gives Yes and No); Ok when empty.</param>
    /// <param name="topLevel">The window that owns the dialog, or <see langword="null"/> for the active one.</param>
    /// <returns>The label of the button that was chosen, or <see langword="null"/> when the dialog was dismissed.</returns>
    Task<string?> ShowMessageBoxAsync(string title, string message, string buttons, TopLevel? topLevel = null);

    /// <summary>Shows a content dialog (a title, a content and up to three buttons) and waits for it to close.</summary>
    /// <param name="params">What the dialog shows and how its buttons behave.</param>
    /// <param name="topLevel">The window that owns the dialog, or <see langword="null"/> for the active one.</param>
    /// <returns>The button that closed the dialog.</returns>
    Task<RouteableContentDialogResult> ShowContentDialogAsync(RouteableContentDialog @params, TopLevel? topLevel = null);

    /// <summary>Shows a task dialog (a message with commands and optional details) and waits for it to close.</summary>
    /// <param name="params">What the dialog shows.</param>
    /// <param name="topLevel">The window that owns the dialog, or <see langword="null"/> for the active one.</param>
    /// <param name="results">The results the dialog may return: they decide which buttons it shows.</param>
    /// <returns>The result that closed the dialog.</returns>
    Task<RouteableTaskDialogResult> ShowTaskDialogAsync(RouteableTaskDialog @params, TopLevel? topLevel = null, params RouteableTaskDialogResult[] results);

    /// <summary>Whether this service can show a dialog on <paramref name="topLevel"/>; when not, the host tries another window.</summary>
    /// <param name="topLevel">The window to test.</param>
    bool IsDisplayable(TopLevel topLevel);
}

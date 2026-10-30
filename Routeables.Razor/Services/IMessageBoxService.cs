using Routeables.Abstraction;
using Routeables.Services;

namespace Routeables.Razor.Services;

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
    /// <returns>The label of the button that was chosen, or <see langword="null"/> when the dialog was dismissed.</returns>
    Task<int> ShowMessageAsync(string title, string message, string buttons);

    /// <summary>Shows a content dialog (a title, a content and up to three buttons) and waits for it to close.</summary>
    /// <param name="params">What the dialog shows and how its buttons behave.</param>
    /// <returns>The button that closed the dialog.</returns>
    Task<RouteableContentDialogResult> ShowContentDialogAsync(RouteableContentDialog @params);

    /// <summary>Shows a task dialog (a message with commands and optional details) and waits for it to close.</summary>
    /// <param name="params">What the dialog shows.</param>
    /// <param name="results">The results the dialog may return: they decide which buttons it shows.</param>
    /// <returns>The result that closed the dialog.</returns>
    Task<RouteableTaskDialogResult> ShowTaskDialogAsync(RouteableTaskDialog @params, params RouteableTaskDialogResult[] results);

    /// <summary>Shows <paramref name="viewFactory"/>'s view as a modal and waits until <paramref name="viewModel"/> closes it.</summary>
    /// <typeparam name="TViewModel">The type of the view model, which asks to be closed.</typeparam>
    /// <typeparam name="TResult">The type of the value the modal returns.</typeparam>
    /// <param name="viewModel">The view model of the modal; its <see cref="ICloseableViewModel{TResult}.Commands"/> become the buttons.</param>
    /// <param name="viewFactory">Creates the content to show.</param>
    /// <returns>The value the view model closed with, or <see langword="default"/> when the modal was dismissed.</returns>
    Task<TResult?> ShowModalAsync<TViewModel, TResult>(
        TViewModel viewModel)
        where TViewModel : ICloseableViewModel<TResult>;
}

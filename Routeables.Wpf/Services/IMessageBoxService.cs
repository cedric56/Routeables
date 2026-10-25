using System.Windows;
using Routeables.Services;

namespace Routeables.Wpf.Services;

/// <summary>
/// Shows the dialogs of the host. <see cref="DefaultMessageBoxService"/> draws them with plain WPF controls; supply your
/// own to use another library. The owner is the window the dialog is shown over; the active window when it is
/// <see langword="null"/>.
/// </summary>
public interface IMessageBoxService
{
    /// <summary>Shows a message with buttons and returns the text of the button pressed, or <see langword="null"/> when the dialog was dismissed.</summary>
    /// <param name="title">The title.</param>
    /// <param name="message">The message.</param>
    /// <param name="buttons">The buttons: words written together, each starting with a capital letter (<c>YesNoCancel</c>).</param>
    /// <param name="owner">The window to show the dialog over.</param>
    Task<string?> ShowMessageBoxAsync(string title, string message, string buttons, Window? owner = null);

    /// <summary>Shows a content dialog.</summary>
    /// <param name="params">The dialog.</param>
    /// <param name="owner">The window to show the dialog over.</param>
    Task<RouteableContentDialogResult> ShowAsync(RouteableContentDialog @params, Window? owner = null);

    /// <summary>Shows a task dialog.</summary>
    /// <param name="params">The dialog.</param>
    /// <param name="owner">The window to show the dialog over.</param>
    /// <param name="results">The results the buttons produce, in order; the defaults of the dialog when empty.</param>
    Task<RouteableTaskDialogResult> ShowAsync(RouteableTaskDialog @params, Window? owner = null, params RouteableTaskDialogResult[] results);

    /// <summary>Gets whether a dialog can be shown over <paramref name="owner"/> now.</summary>
    bool IsDisplayable(Window owner);
}

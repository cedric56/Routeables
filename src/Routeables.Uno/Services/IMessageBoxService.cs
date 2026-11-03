using Microsoft.UI.Xaml;
using Routeables.Services;

namespace Routeables.Uno.Services;

/// <summary>
/// Draws the dialogs of the services of this package. <see cref="DefaultMessageBoxService"/> uses a <c>ContentDialog</c>; give
/// <c>UseRouteablesServices</c> another implementation to draw them your own way.
/// </summary>
public interface IMessageBoxService
{
    /// <summary>
    /// Shows a message with buttons and returns the text of the one chosen, or <see langword="null"/> when the dialog was dismissed.
    /// </summary>
    /// <param name="title">The title.</param>
    /// <param name="message">The message.</param>
    /// <param name="buttons">The texts of the buttons written together, one word per capital letter: <c>"YesNoCancel"</c> is Yes, No and Cancel.</param>
    /// <param name="root">The root of the window that shows the dialog; <see langword="null"/> is the active window.</param>
    Task<string?> ShowMessageBoxAsync(string title, string message, string buttons, XamlRoot? root = null);

    /// <summary>Shows a dialog that hosts a content, with up to three buttons.</summary>
    /// <param name="params">What to show.</param>
    /// <param name="root">The root of the window that shows the dialog; <see langword="null"/> is the active window.</param>
    Task<RouteableContentDialogResult> ShowAsync(RouteableContentDialog @params, XamlRoot? root = null);

    /// <summary>Shows a task dialog, with one button per requested result.</summary>
    /// <param name="params">What to show.</param>
    /// <param name="root">The root of the window that shows the dialog; <see langword="null"/> is the active window.</param>
    /// <param name="results">The results to offer as buttons; none offers <see cref="RouteableTaskDialogResult.Close"/>.</param>
    Task<RouteableTaskDialogResult> ShowAsync(RouteableTaskDialog @params, XamlRoot? root = null, params RouteableTaskDialogResult[] results);

    /// <summary>Whether a dialog can be shown in the window of <paramref name="root"/> (it is not hidden).</summary>
    bool IsDisplayable(XamlRoot root);
}

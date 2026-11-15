using System.Windows.Input;

namespace Routeables.Services;

/// <summary>
/// Keeps a dialog button enabled while the command it is tied to can execute, whatever the host draws the button with: the state is
/// computed once, then again each time the command says it changed. Every host gives the way to set its button and to go to its interface thread.
/// </summary>
internal static class RouteableCommandFollower
{
    /// <summary>Sets the state of the button now and follows the command until the action added to <paramref name="cleanup"/> runs.</summary>
    /// <param name="command">The command that validates the button; without one only <paramref name="isEnabled"/> counts.</param>
    /// <param name="parameter">The parameter passed to <c>CanExecute</c>.</param>
    /// <param name="isEnabled">Whether the button is enabled at all, whatever the command says.</param>
    /// <param name="setEnabled">Sets the button; always called on the interface thread (see <paramref name="post"/>).</param>
    /// <param name="post">Runs an action on the interface thread, at once when already there: <c>CanExecuteChanged</c> can be raised from any thread.</param>
    /// <param name="cleanup">Receives the action that stops following the command; run it when the dialog closes.</param>
    public static void Follow(ICommand? command, object? parameter, bool isEnabled, Action<bool> setEnabled, Action<Action> post, List<Action> cleanup)
    {
        ArgumentNullException.ThrowIfNull(setEnabled);
        ArgumentNullException.ThrowIfNull(post);
        ArgumentNullException.ThrowIfNull(cleanup);

        void Refresh() => setEnabled(isEnabled && (command?.CanExecute(parameter) ?? true));
        Refresh();

        if (command is null)
            return;

        void OnCanExecuteChanged(object? sender, EventArgs e) => post(Refresh);
        command.CanExecuteChanged += OnCanExecuteChanged;
        cleanup.Add(() => command.CanExecuteChanged -= OnCanExecuteChanged);
    }
}

using Routeables.Abstraction;

namespace Routeables.Services;

/// <summary>
/// What a task dialog does, whatever the host draws it with: which buttons it has, what pressing one does and when the
/// dialog may close. A host draws <see cref="Buttons"/> and the content, forwards the events of its surface to
/// <see cref="TryDismiss"/>, <see cref="Opened"/> and <see cref="Release"/>, and reads <see cref="Result"/> once the
/// dialog is closed.
/// </summary>
/// <remarks>Pressing a button asks <c>Closing</c>, which returns <see langword="true"/> to keep the dialog open.</remarks>
public sealed class RouteableTaskDialogSession
{
    private readonly RouteableTaskDialog _dialog;
    private readonly Action _complete;

    /// <summary>Starts the session of <paramref name="dialog"/>.</summary>
    /// <param name="dialog">The dialog to show.</param>
    /// <param name="results">The results to offer as buttons; none offers <see cref="RouteableTaskDialogResult.Close"/>. <see cref="RouteableTaskDialogResult.None"/> is not a button.</param>
    /// <param name="complete">Closes the surface of the dialog; called once a button decided the result.</param>
    public RouteableTaskDialogSession(RouteableTaskDialog dialog, RouteableTaskDialogResult[]? results, Action complete)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        ArgumentNullException.ThrowIfNull(complete);
        _dialog = dialog;
        _complete = complete;

        var wanted = results is { Length: > 0 } ? results : [RouteableTaskDialogResult.Close];
        Buttons = [.. wanted
            .Where(r => r != RouteableTaskDialogResult.None)
            .Select((r, index) => new RouteableDialogButton(r.ToString(), true, null, null, index == 0, () => Submit(r)))];
    }

    /// <summary>The result of the dialog: <see cref="RouteableTaskDialogResult.None"/> until a button decided it.</summary>
    public RouteableTaskDialogResult Result { get; private set; } = RouteableTaskDialogResult.None;

    /// <summary>The buttons to draw, one per result asked; the first one is the default.</summary>
    public IReadOnlyList<RouteableDialogButton> Buttons { get; }

    /// <summary>Tells the dialog it is about to open (<c>Opening</c>).</summary>
    public void Opening() => RouteableErrors.Guard(null, "Dialog.Opening", _dialog.Opening);

    /// <summary>Tells the dialog it is on screen (<c>Opened</c>).</summary>
    public void Opened() => RouteableErrors.Guard(null, "Dialog.Opened", _dialog.Opened);

    /// <summary>
    /// The user dismissed the dialog (Escape, a click outside). Asks <c>Closing</c>; when it allows it the result is
    /// <see cref="RouteableTaskDialogResult.None"/>. Returns <see langword="false"/> to keep the dialog open.
    /// </summary>
    public bool TryDismiss()
    {
        if (RouteableErrors.Guard(null, "TaskDialog.Closing", _dialog.Closing))
            return false;

        Result = RouteableTaskDialogResult.None;
        return true;
    }

    /// <summary>The dialog is closed: <c>Closed</c> is told. Call it once, whatever the way the dialog closed.</summary>
    public void Release() => RouteableErrors.Guard(null, "Dialog.Closed", _dialog.Closed);

    private bool Submit(RouteableTaskDialogResult requested)
    {
        if (RouteableErrors.Guard(null, "TaskDialog.Closing", _dialog.Closing))
            return false;

        Result = requested;
        _complete();
        return true;
    }
}

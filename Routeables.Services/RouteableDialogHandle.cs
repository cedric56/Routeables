using Routeables.Abstraction;

namespace Routeables.Services;

/// <summary>
/// Connects the logic of a dialog (what each button returns, whether closing may be vetoed) with the surface that
/// displays it (a window, an overlay, a <c>ContentDialog</c>), whatever the host. The logic calls <see cref="Complete"/>;
/// the surface sets <see cref="CloseSurface"/>, asks <see cref="AcceptDismiss"/> when the user dismisses the dialog and
/// calls <see cref="NotifyOpened"/> once the dialog is on screen.
/// </summary>
internal sealed class RouteableDialogHandle
{
    private bool _surfaceClosing;

    /// <summary>Whether the dialog has already decided its result and is closing.</summary>
    public bool Completed { get; private set; }

    /// <summary>
    /// Asked when the user dismisses the dialog (Escape, close button of the window). Returns <see langword="false"/> to
    /// keep the dialog open. When it returns <see langword="true"/> it must have recorded the dismissal result.
    /// </summary>
    public Func<bool>? TryDismiss { get; set; }

    /// <summary>Invoked once the dialog is on screen.</summary>
    public Action? Opened { get; set; }

    /// <summary>Set by the surface while it is shown: closes the dialog. Set it to <see langword="null"/> when the dialog is gone.</summary>
    public Action? CloseSurface { get; set; }

    /// <summary>Closes the dialog after a button decided the result; as soon as it is open when it is not yet.</summary>
    public void Complete()
    {
        if (Completed) return;

        Completed = true;
        CloseNow();
    }

    /// <summary>
    /// Handles a dismissal request. Returns <see langword="true"/> when the dismissal is accepted; the caller then lets
    /// the surface close (a window closes by itself, an overlay is closed through <see cref="RequestDismiss"/>).
    /// </summary>
    public bool AcceptDismiss()
    {
        if (Completed) return true;
        if (TryDismiss?.Invoke() == false) return false;

        Completed = true;
        return true;
    }

    /// <summary>Dismisses the dialog and closes the surface, unless the dismissal is vetoed.</summary>
    public bool RequestDismiss()
    {
        if (!AcceptDismiss()) return false;

        CloseNow();
        return true;
    }

    /// <summary>Called by the surface once the dialog is on screen: tells <see cref="Opened"/>, then closes the dialog if the code completed it meanwhile.</summary>
    public void NotifyOpened()
    {
        Opened?.Invoke();
        if (Completed)
            CloseNow();
    }

    private void CloseNow()
    {
        if (_surfaceClosing || CloseSurface is not { } close) return;

        _surfaceClosing = true;
        close();
    }
}

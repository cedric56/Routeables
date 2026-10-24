namespace Routeables.Avalonia.Services;

/// <summary>
/// Connects the logic of a dialog (what each button returns, whether closing may be vetoed) with the surface
/// that displays it (an owned window, or an overlay on a single-view top level).
/// </summary>
internal sealed class DialogHandle
{
    /// <summary>Whether the dialog has already decided its result and is closing.</summary>
    public bool Completed { get; private set; }

    /// <summary>
    /// Asked when the user dismisses the dialog (Escape, window close button). Returns <see langword="false"/>
    /// to keep the dialog open. When it returns <see langword="true"/> it must have recorded the dismissal result.
    /// </summary>
    public Func<bool>? TryDismiss { get; set; }

    /// <summary>Invoked once the dialog is on screen.</summary>
    public Action? Opened { get; set; }

    /// <summary>Set by the surface: closes the dialog.</summary>
    internal Action? CloseSurface { get; set; }

    /// <summary>Closes the dialog after a button decided the result.</summary>
    public void Complete()
    {
        if (Completed) return;

        Completed = true;
        CloseSurface?.Invoke();
    }

    /// <summary>
    /// Handles a dismissal request. Returns <see langword="true"/> when the dismissal is accepted; the caller then
    /// lets the surface close (a window closes by itself, an overlay is closed through <see cref="RequestDismiss"/>).
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

        CloseSurface?.Invoke();
        return true;
    }
}
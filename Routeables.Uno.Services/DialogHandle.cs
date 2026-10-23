namespace Routeables.Uno.Services;

/// <summary>The link between a dialog on screen and the code that decides when it closes.</summary>
internal sealed class DialogHandle
{
    private bool _completeRequested;

    /// <summary>Called once the dialog is open.</summary>
    public Action? Opened { get; set; }

    /// <summary>Asked when the user dismisses the dialog (Escape): returns <see langword="false"/> to keep it open.</summary>
    public Func<bool>? TryDismiss { get; set; }

    /// <summary>Closes the dialog; set by the presenter while it is shown.</summary>
    internal Action? Hide { get; set; }

    /// <summary>Set when the code, not the user, closed the dialog: the dismissal is then not asked again.</summary>
    internal bool Completing { get; private set; }

    internal bool CompleteRequested => _completeRequested;

    /// <summary>Closes the dialog now, or as soon as it is open.</summary>
    public void Complete()
    {
        _completeRequested = true;
        Completing = true;
        Hide?.Invoke();
    }
}

using Routeables.Abstraction;
using System.Windows.Input;

namespace Routeables.Services;

/// <summary>
/// What a content dialog does, whatever the host draws it with: which buttons it has, what pressing one does and when the
/// dialog may close. A host draws <see cref="Buttons"/> and the content, forwards the events of its surface to
/// <see cref="TryDismiss"/>, <see cref="Opened"/> and <see cref="Release"/>, and reads <see cref="Result"/> once the
/// dialog is closed.
/// </summary>
/// <remarks>
/// Pressing a button runs its command (or, without one, its click callback), then asks <c>Closing</c>, which returns
/// <see langword="true"/> to keep the dialog open, for example while a view model has not asked to close yet. Only then
/// does the dialog close with the result of the button.
/// </remarks>
public sealed class RouteableContentDialogSession
{
    private readonly RouteableContentDialog _dialog;
    private readonly Action _complete;

    /// <summary>Starts the session of <paramref name="dialog"/>.</summary>
    /// <param name="dialog">The dialog to show.</param>
    /// <param name="complete">Closes the surface of the dialog; called once a button, or the code, decided the result.</param>
    public RouteableContentDialogSession(RouteableContentDialog dialog, Action complete)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        ArgumentNullException.ThrowIfNull(complete);
        _dialog = dialog;
        _complete = complete;
        Buttons = CreateButtons();
    }

    /// <summary>The result of the dialog: <see cref="RouteableContentDialogResult.None"/> until a button decided it.</summary>
    public RouteableContentDialogResult Result { get; private set; } = RouteableContentDialogResult.None;

    /// <summary>The buttons to draw, in the order of the dialog: primary, secondary, close. A button without a text is not there.</summary>
    public IReadOnlyList<RouteableDialogButton> Buttons { get; }

    /// <summary>Tells the dialog it is about to open (<c>Opening</c>).</summary>
    public void Opening() => RouteableErrors.Guard(null, "Dialog.Opening", _dialog.Opening);

    /// <summary>Tells the dialog it is on screen (<c>Opened</c>).</summary>
    public void Opened() => RouteableErrors.Guard(null, "Dialog.Opened", _dialog.Opened);

    /// <summary>
    /// The user dismissed the dialog (Escape, a click outside). Asks <c>Closing</c>; when it allows it the result is
    /// <see cref="RouteableContentDialogResult.None"/>. Returns <see langword="false"/> to keep the dialog open.
    /// </summary>
    public bool TryDismiss()
    {
        if (RouteableErrors.Guard(null, "ContentDialog.Closing", _dialog.Closing, nameof(RouteableContentDialogResult.None)))
            return false;

        Result = RouteableContentDialogResult.None;
        return true;
    }

    /// <summary>
    /// Lets the caller close the dialog later with <see cref="RouteableContentDialog.Close"/>.
    /// </summary>
    /// <param name="runOnUiThread">Runs the action it is given on the thread of the dialog; the caller may be on any thread.</param>
    public void AllowCloseFromCode(Action<Action> runOnUiThread)
    {
        ArgumentNullException.ThrowIfNull(runOnUiThread);
        _dialog.Close = () => runOnUiThread(() =>
        {
            Result = RouteableContentDialogResult.None;
            _complete();
        });
    }

    /// <summary>
    /// The dialog is closed: the caller can no longer close it from code, and <c>Closed</c> is told. Call it once, whatever
    /// the way the dialog closed.
    /// </summary>
    public void Release()
    {
        _dialog.Close = null;
        RouteableErrors.Guard(null, "Dialog.Closed", _dialog.Closed);
    }

    private List<RouteableDialogButton> CreateButtons()
    {
        var buttons = new List<RouteableDialogButton>();
        Add(_dialog.PrimaryButtonText, _dialog.IsPrimaryButtonEnabled, RouteableContentDialogResult.Primary,
            _dialog.PrimaryButtonCommand, _dialog.PrimaryButtonCommandParameter, _dialog.PrimaryButtonClick, isDefault: true);
        Add(_dialog.SecondaryButtonText, _dialog.IsSecondaryButtonEnabled, RouteableContentDialogResult.Secondary,
            _dialog.SecondaryButtonCommand, _dialog.SecondaryButtonCommandParameter, _dialog.SecondaryButtonClick);
        Add(_dialog.CloseButtonText, true, RouteableContentDialogResult.None,
            _dialog.CloseButtonCommand, _dialog.CloseButtonCommandParameter, _dialog.CloseButtonClick);
        return buttons;

        void Add(string? text, bool isEnabled, RouteableContentDialogResult requested, ICommand? command, object? parameter, Action? click, bool isDefault = false)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            buttons.Add(new RouteableDialogButton(text, isEnabled, command, parameter, isDefault, () => Submit(requested, command, parameter, click)));
        }
    }

    private bool Submit(RouteableContentDialogResult requested, ICommand? command, object? parameter, Action? click)
    {
        if (command is not null && !command.CanExecute(parameter))
            return false;

        if (command is not null)
            command.Execute(parameter);
        else
            RouteableErrors.Guard(null, "ContentDialog.Click", click);

        if (RouteableErrors.Guard(null, "ContentDialog.Closing", _dialog.Closing, requested.ToString()))
            return false;

        Result = requested;
        _complete();
        return true;
    }
}

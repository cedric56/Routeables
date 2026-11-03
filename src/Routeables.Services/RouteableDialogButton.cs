using System.Windows.Input;

namespace Routeables.Services;

/// <summary>
/// One button of a dialog, as the session of the dialog describes it. A host draws it with its own controls and calls
/// <see cref="Press"/> when the user presses it.
/// </summary>
public sealed class RouteableDialogButton
{
    private readonly Func<bool> _press;

    internal RouteableDialogButton(string text, bool isEnabled, ICommand? command, object? parameter, bool isDefault, Func<bool> press)
    {
        Text = text;
        IsEnabled = isEnabled;
        Command = command;
        CommandParameter = parameter;
        IsDefault = isDefault;
        _press = press;
    }

    /// <summary>The text of the button.</summary>
    public string Text { get; }

    /// <summary>Whether the button is enabled at all, whatever its command says.</summary>
    public bool IsEnabled { get; }

    /// <summary>The command that validates the button: it cannot be pressed while the command cannot execute. <see langword="null"/> when there is none.</summary>
    public ICommand? Command { get; }

    /// <summary>The parameter passed to <see cref="Command"/>.</summary>
    public object? CommandParameter { get; }

    /// <summary>Whether Enter activates this button.</summary>
    public bool IsDefault { get; }

    /// <summary>Presses the button. Returns <see langword="true"/> when the dialog closes because of it.</summary>
    public bool Press() => _press();
}

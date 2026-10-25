using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Routeables.Abstraction;

/// <summary>
/// A view model shown in a modal: a dialog-like view that gives a result back. It names itself, offers its buttons and asks to be
/// closed with a value; the modal service of the host shows it and returns that value to the caller of <c>ShowModalAsync</c>.
/// </summary>
/// <typeparam name="TResult">The type of the value the modal returns when it closes.</typeparam>
public interface ICloseableViewModel<TResult>
{
    /// <summary>The title the modal shows.</summary>
    string Title { get; }

    /// <summary>The buttons the modal shows (Close, Save, …), each with its command.</summary>
    ObservableCollection<RouteableCommand> Commands { get; }

    /// <summary>The command that closes the modal, or <see langword="null"/> when it is closed only through its other buttons.</summary>
    ICommand? CloseCommand { get; }

    /// <summary>Raised by <see cref="CloseAsync"/>: the modal service listens to it, closes the modal and returns the value.</summary>
    event EventHandler<TResult?>? CloseRequested;

    /// <summary>Asks for the modal to close; <paramref name="value"/> is what the caller of <c>ShowModalAsync</c> receives.</summary>
    /// <param name="value">The result of the modal, or <see langword="default"/> when it was dismissed.</param>
    Task CloseAsync(TResult? value);
}
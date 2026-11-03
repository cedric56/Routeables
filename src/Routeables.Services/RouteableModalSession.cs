using Routeables.Abstraction;

namespace Routeables.Services;

/// <summary>
/// What a modal dialog backed by a closeable view model does, whatever the host draws it with: the content dialog that
/// shows its view and commands, the result the view model closes it with, and the end of the subscription to the view
/// model. A host shows <see cref="Dialog"/> with its content dialog service and then reads <see cref="Result"/>.
/// </summary>
/// <remarks>
/// The first command becomes the primary button, the second the secondary button and, when there are more than two, the
/// last one the close button. A view model without commands gets a plain "Close" button, so the dialog can always be
/// left. Pressing the primary or secondary button runs its command; the dialog then stays open until the view model asks
/// to close (<see cref="ICloseableViewModel{TResult}.CloseAsync"/>), so a command that finds the input invalid can show a
/// message and leave the dialog open. The close button, and Escape, close the dialog as long as the close command (if
/// any) can execute. Dispose the session once the dialog is closed: a dialog dismissed without a close request would
/// otherwise leave the handler on the view model.
/// </remarks>
/// <typeparam name="TResult">The type of result the view model closes with.</typeparam>
internal sealed class RouteableModalSession<TResult> : IDisposable
{
    private readonly ICloseableViewModel<TResult?> _viewModel;
    private bool _closeRequested;

    /// <summary>Starts the session and listens to the close request of the view model.</summary>
    /// <param name="viewModel">The view model that supplies the title and the commands, and closes the dialog.</param>
    /// <param name="content">What the dialog shows: the view of the view model.</param>
    public RouteableModalSession(ICloseableViewModel<TResult?> viewModel, object? content)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _viewModel = viewModel;

        // Subscribed before the dialog is shown: a view model that closes right away must not be missed.
        _viewModel.CloseRequested += OnCloseRequested;
        Dialog = CreateDialog(viewModel, content);
    }

    /// <summary>The content dialog to show.</summary>
    public RouteableContentDialog Dialog { get; }

    /// <summary>The value the view model closed the dialog with; the default value when the dialog was dismissed without one.</summary>
    public TResult? Result { get; private set; }

    /// <summary>Whether the view model has asked to close.</summary>
    public bool CloseRequested => _closeRequested;

    /// <summary>Stops listening to the view model.</summary>
    public void Dispose() => _viewModel.CloseRequested -= OnCloseRequested;

    private void OnCloseRequested(object? sender, TResult? value)
    {
        Result = value;
        _closeRequested = true;

        // Close the dialog when the request comes after the buttons were pressed (an asynchronous command).
        Dialog?.Close?.Invoke();
    }

    private RouteableContentDialog CreateDialog(ICloseableViewModel<TResult?> viewModel, object? content)
    {
        var commands = viewModel.Commands;
        var primary = commands.ElementAtOrDefault(0);
        var secondary = commands.ElementAtOrDefault(1);
        var close = commands.Count > 2 ? commands[^1] : null;

        var dialog = new RouteableContentDialog
        {
            Content = content,
            Title = viewModel.Title,
            PrimaryButtonText = primary?.Name,
            PrimaryButtonCommand = primary?.Command,
            IsPrimaryButtonEnabled = true,
            SecondaryButtonText = secondary?.Name,
            SecondaryButtonCommand = secondary?.Command,
            IsSecondaryButtonEnabled = true,
            CloseButtonText = close?.Name,
            CloseButtonCommand = close?.Command
        };

        if (commands.Count == 0)
            dialog.CloseButtonText = "Close";

        // Closing returns true to keep the dialog open.
        dialog.Closing = button => button switch
        {
            nameof(RouteableContentDialogResult.Primary) => !_closeRequested,
            nameof(RouteableContentDialogResult.Secondary) => !_closeRequested,
            _ => !_closeRequested && !CanExecute(dialog.CloseButtonCommand, dialog.CloseButtonCommandParameter)
        };

        return dialog;

        static bool CanExecute(System.Windows.Input.ICommand? command, object? parameter) =>
            command?.CanExecute(parameter) ?? true;
    }
}

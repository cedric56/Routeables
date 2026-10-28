using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Routeables.Abstraction;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Routeables.Core;

/// <summary>
/// Represents an abstract base view model supporting close operations and result returns,
/// integrated with ReactiveUI commands.
/// </summary>
/// <typeparam name="TResult">The type of result returned when closing.</typeparam>
[IReactiveObject]
public abstract partial class CloseableViewModel<TResult>
    : CloseableViewModelBase<TResult>
{
    private ICommand? _closeCommand = null;

    /// <summary>
    /// Gets the command executed to attempt closing the view model, subject to <see cref="CloseableViewModelBase{TResult}.CanCloseAsync()"/> validation.
    /// </summary>
    public override ICommand CloseCommand => _closeCommand ??= ReactiveCommand.CreateFromTask(async () =>
    {
        if (await CanCloseAsync())
            await CloseAsync(default);
    });
}

/// <summary>
/// Represents an abstract base view model with built-in routing history support (<see cref="GoBackAsync"/> and <see cref="GoForwardAsync"/>)
/// managed by a router.
/// </summary>
/// <param name="router">The router managing routing state and history.</param>
/// <param name="initialize">A value indicating whether the view model should initialize immediately upon construction.</param>
[IReactiveObject]
public abstract partial class RouteableViewModel(IRouter router, bool initialize = true) : RouteableViewModelBase(router, initialize)
{
    /// <summary>
    /// Goes backward in the route change history asynchronously.
    /// </summary>
    [ReactiveCommand]
    public override Task GoBackAsync()
    {
        return base.GoBackAsync();
    }

    /// <summary>
    /// Goes forward in the route change history asynchronously.
    /// </summary>
    [ReactiveCommand]
    public override Task GoForwardAsync()
    {
        return base.GoForwardAsync();
    }

    /// <summary>
    /// The two commands have no <c>canExecute</c> condition (as with the CommunityToolkit flavour), so there is nothing to notify.
    /// </summary>
    protected override void RaiseCanExecutesChanged()
    {
    }

    /// <summary>
    /// Raises property change notifications for the specified property name.
    /// </summary>
    /// <param name="propertyName">The name of the property that changed.</param>
    protected override void NotifyPropertyChanged(string? propertyName)
    {
        this.RaisePropertyChanged(propertyName);
    }
}

/// <summary>
/// Represents an abstract routeable view model that can return a result when closed.
/// Provides common commands (Validate, Cancel, Close) and a title.
/// </summary>
/// <typeparam name="TResult">The type of the result returned when the view model is closed.</typeparam>
/// <param name="router">The router that manages routing state and history.</param>
/// <param name="initialize">
/// A value indicating whether the view model should initialize immediately upon construction.
/// </param>
public abstract partial class RouteableViewModel<TResult>(IRouter router, bool initialize = true)
    : RouteableViewModel(router, initialize), ICloseableViewModel<TResult?>
{
    /// <summary>
    /// Gets the title of the view model.
    /// </summary>
    public abstract string Title { get; }

    private ObservableCollection<RouteableCommand>? _commands;

    /// <summary>
    /// Creates the collection of commands available for this view model.
    /// Override this method to customize the available commands.
    /// </summary>
    /// <returns>A collection of <see cref="RouteableCommand"/> instances.</returns>
    protected virtual ObservableCollection<RouteableCommand> GetCommands()
    {
        return new ObservableCollection<RouteableCommand>()
        {
            new RouteableCommand()
            {
                Name = "Validate",
                Command = ValidateCommand
            },
            new RouteableCommand()
            {
                Name = "Cancel",
                Command = CancelCommand
            },
            new RouteableCommand()
            {
                Name = "Close",
                Command = CloseCommand
            }
        };
    }

    /// <summary>
    /// Gets the collection of commands available for this view model.
    /// </summary>
    public ObservableCollection<RouteableCommand> Commands => _commands ??= GetCommands();

    /// <inheritdoc/>
    ICommand? ICloseableViewModel<TResult?>.CloseCommand => CloseCommand;

    /// <summary>
    /// Validates the current state of the view model asynchronously.
    /// </summary>
    /// <returns>A task that represents the asynchronous validation operation.</returns>
    [ReactiveCommand]
    public abstract Task ValidateAsync();

    /// <summary>
    /// Cancels the current operation and closes the view model if allowed.
    /// </summary>
    /// <returns>A task that represents the asynchronous cancel operation.</returns>
    [ReactiveCommand]
    public virtual async Task CancelAsync()
    {
        if (await CanCancelAsync())
            await CloseAsync(default);
    }

    /// <summary>
    /// Determines whether the cancel operation can be performed.
    /// </summary>
    /// <returns>
    /// A task that returns <see langword="true"/> if cancellation is allowed; otherwise, <see langword="false"/>.
    /// </returns>
    protected virtual Task<bool> CanCancelAsync()
    {
        return Task.FromResult(true);
    }

    /// <summary>
    /// Determines whether the view model can be closed.
    /// </summary>
    /// <returns>
    /// A task that returns <see langword="true"/> if closing is allowed; otherwise, <see langword="false"/>.
    /// </returns>
    protected virtual Task<bool> CanCloseAsync()
    {
        return Task.FromResult(true);
    }

    /// <summary>
    /// Occurs when a close is requested, providing the optional result value.
    /// </summary>
    public event EventHandler<TResult?>? CloseRequested;

    /// <summary>
    /// Attempts to close the view model after checking <see cref="CanCloseAsync"/>.
    /// </summary>
    /// <returns>A task that represents the asynchronous close operation.</returns>
    [ReactiveCommand]
    public async Task CloseAsync()
    {
        if (await CanCloseAsync())
            await CloseAsync(default);
    }

    /// <summary>
    /// Closes the view model and raises the <see cref="CloseRequested"/> event with the specified result.
    /// </summary>
    /// <param name="value">The result value to return when closing.</param>
    /// <returns>A completed task.</returns>
    public Task CloseAsync(TResult? value)
    {
        CloseRequested?.Invoke(this, value);
        return Task.CompletedTask;
    }
}
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Routeables.Abstraction;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Routeables.Core;

/// <summary>
/// Represents an abstract base view model that supports close operations and result returns,
/// integrated with CommunityToolkit.Mvvm commands.
/// </summary>
/// <typeparam name="TResult">The type of the result returned when the view model is closed.</typeparam>
[INotifyPropertyChanged]
public abstract partial class CloseableViewModel<TResult>
    : CloseableViewModelBase<TResult>
{
    private IAsyncRelayCommand? _closeCommand;

    /// <summary>
    /// Gets the command used to attempt closing the view model.
    /// The command first checks <see cref="CloseableViewModelBase{TResult}.CanCloseAsync"/> before closing.
    /// </summary>
    public override ICommand CloseCommand => _closeCommand ??= new AsyncRelayCommand(async () =>
    {
        if (await CanCloseAsync())
            await CloseAsync(default);
    });
}

/// <summary>
/// Represents an abstract base view model with built-in routing history support
/// (<see cref="GoBackAsync"/> and <see cref="GoForwardAsync"/>) managed by a router.
/// </summary>
/// <param name="router">The router that manages routing state and history.</param>
/// <param name="initialize">
/// A value indicating whether the view model should initialize immediately upon construction.
/// </param>
[INotifyPropertyChanged]
public abstract partial class RouteableViewModel(IRouter router, bool initialize = true)
    : RouteableViewModelBase(router, initialize)
{
    /// <summary>
    /// Navigates backward in the route change history asynchronously.
    /// </summary>
    /// <returns>A task that represents the asynchronous navigation operation.</returns>
    [RelayCommand]
    public override Task GoBackAsync()
    {
        return base.GoBackAsync();
    }

    /// <summary>
    /// Navigates forward in the route change history asynchronously.
    /// </summary>
    /// <returns>A task that represents the asynchronous navigation operation.</returns>
    [RelayCommand]
    public override Task GoForwardAsync()
    {
        return base.GoForwardAsync();
    }

    /// <summary>
    /// Notifies that the execution state of the routing commands has changed.
    /// </summary>
    protected override void RaiseCanExecutesChanged()
    {
        GoBackCommand.NotifyCanExecuteChanged();
        GoForwardCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Raises a property change notification for the specified property.
    /// </summary>
    /// <param name="propertyName">The name of the property that changed.</param>
    protected override void NotifyPropertyChanged(string? propertyName)
    {
        OnPropertyChanged(propertyName);
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
    [RelayCommand]
    public abstract Task ValidateAsync();

    /// <summary>
    /// Cancels the current operation and closes the view model if allowed.
    /// </summary>
    /// <returns>A task that represents the asynchronous cancel operation.</returns>
    [RelayCommand]
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
    [RelayCommand]
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
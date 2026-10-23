using Routeables.Abstraction;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Routeables.Core;

/// <summary>
/// Base class for a view model that can be closed and return a result value, without routing support.
/// </summary>
/// <typeparam name="TResult">The type of the result produced when this view model is closed.</typeparam>
public abstract partial class CloseableViewModelBase<TResult> : ICloseableViewModel<TResult>
{
    /// <summary>
    /// Occurs when a close has been requested for this view model, carrying the resulting value, if any.
    /// </summary>
    public event EventHandler<TResult?>? CloseRequested;

    /// <summary>
    /// Gets the command that, when executed, closes the view model with a default result value.
    /// </summary>
    public abstract ICommand CloseCommand { get; }

    /// <summary>
    /// Gets the collection of commands exposed by this view model. Empty by default.
    /// </summary>
    public virtual ObservableCollection<RouteableCommand> Commands { get; } = [];

    /// <summary>
    /// Gets the display title for this view model.
    /// </summary>
    public abstract string Title { get; }

    public virtual Task<bool> CanCloseAsync() => Task.FromResult(true);

    /// <summary>
    /// Closes this view model, raising <see cref="CloseRequested"/> with the supplied result value.
    /// </summary>
    /// <param name="value">The result value to pass to subscribers of <see cref="CloseRequested"/>.</param>
    /// <returns>A completed task.</returns>
    public Task CloseAsync(TResult? value)
    {
        CloseRequested?.Invoke(this, value);
        return Task.CompletedTask;
    }
}
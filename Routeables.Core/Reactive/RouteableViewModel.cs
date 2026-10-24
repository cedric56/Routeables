using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Routeables.Abstraction;
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
    protected override void NotifyPropertyChanged(string propertyName)
    {
        this.RaisePropertyChanged(propertyName);
    }
}
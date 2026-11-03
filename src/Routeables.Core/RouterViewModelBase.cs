using Routeables.Abstraction;
using Routeables.Routing;

namespace Routeables.Core;

/// <summary>
/// Base class for a view model that supports back/forward routing through a <see cref="Router"/>'s history.
/// </summary>
/// <remarks>
/// <b>This class does not implement <see cref="System.ComponentModel.INotifyPropertyChanged"/>: a class derived from it must</b> (see
/// <see cref="RouteableViewModelBase"/>): <c>[INotifyPropertyChanged]</c> or <c>[ObservableObject]</c> of CommunityToolkit.Mvvm,
/// <c>[IReactiveObject]</c> of ReactiveUI, or by hand.
/// </remarks>
public abstract class RouterViewModelBase
{
    /// <summary>
    /// The router used to route backward and forward through view model history.
    /// </summary>
    protected IRouter _router;

    /// <summary>
    /// Initializes a new instance of the <see cref="RouterViewModelBase"/> class.
    /// </summary>
    /// <param name="router">The router used to manage routing history.</param>
    public RouterViewModelBase(IRouter router)
    {
        _router = router;
    }

    /// <summary>
    /// Goes to the previous view model in the router's history, if available.
    /// <see cref="OnViewModelChanged"/> is only called when the route change succeeded (it is not when a guard
    /// cancelled it), and receives the view model that is now current.
    /// </summary>
    public virtual async Task GoBackAsync()
    {
        if (!CanGoBack())
            return;

        var result = await _router.GoBackAsync();
        if (result.Success)
            OnViewModelChanged(_router.CurrentViewModel);
    }

    /// <summary>
    /// Determines whether backward routing is currently possible.
    /// </summary>
    /// <returns><see langword="true"/> if there is history to go back to; otherwise, <see langword="false"/>.</returns>
    public bool CanGoBack()
    {
        return _router.CanGoBack;
    }

    /// <summary>
    /// Goes to the next view model in the router's history, if available.
    /// </summary>
    public virtual async Task GoForwardAsync()
    {
        if (!CanGoForward())
            return;

        var result = await _router.ForwardAsync();
        if (result.Success)
            OnViewModelChanged(_router.CurrentViewModel);
    }

    /// <summary>
    /// Determines whether forward routing is currently possible.
    /// </summary>
    /// <returns><see langword="true"/> if there is history to go forward to; otherwise, <see langword="false"/>.</returns>
    public bool CanGoForward()
    {
        return _router.CanGoForward;
    }

    /// <summary>
    /// Called whenever routing changes the active view model, e.g. via <see cref="GoBackAsync"/> or <see cref="GoForwardAsync"/>.
    /// The base implementation refreshes command availability.
    /// </summary>
    /// <param name="viewModel">The view model that has become active.</param>
    protected virtual void OnViewModelChanged(object? viewModel)
    {
        RaiseCanExecutesChanged();
    }

    /// <summary>
    /// When implemented in a derived class, notifies the UI that the executability of commands
    /// (e.g. those depending on <see cref="CanGoBack"/> or <see cref="CanGoForward"/>) may have changed.
    /// </summary>
    protected abstract void RaiseCanExecutesChanged();
}
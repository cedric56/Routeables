using Routeables.Abstraction;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Routeables.Core;

/// <summary>
/// The main view model of an application that has no menu, the one the hosts register when <c>UseRouteables</c> is called without a
/// type. It lists no entry. The first page is chosen where the pages appear: a <c>RouteableRegion</c> with a <c>Route</c> opens it,
/// and the default region without one asks this view model to open "/" (<see cref="IMainViewModel.Start"/>).
/// </summary>
/// <param name="router">The router that shows the pages.</param>
internal sealed class DefaultMainViewModel(IRouter router) : MainViewModelBase(router, initialize: false), INotifyPropertyChanged, IMainViewModel
{
    private const string HomeRoute = "/";

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <inheritdoc />
    protected override void NotifyPropertyChanged(string? propertyName)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    /// <summary>No entry: the application has no menu.</summary>
    protected override ObservableCollection<IRouteableEntry> GetRouteables() => [];

    private int _started;

    // The default region calls it when it appears and has no Route of its own. SelectFirst is not overridden: with no entry it selects nothing.
    bool IMainViewModel.Start()
    {
        if (_router.CurrentView is not null || Interlocked.Exchange(ref _started, 1) == 1)
            return false;

        StartAsync().Forget();
        return true;
    }

    private async Task StartAsync()
    {
        try
        {
            var result = await _router.GoToAsync(HomeRoute);
            if (result.Success)
                OnRouterMoved(_router.CurrentViewModel);
            else
                OnSelectionFailed(result.Exception ?? new InvalidOperationException(
                    $"The route '{HomeRoute}' could not be opened. Give the region the page to show: <RouteableRegion Route=\"/your/page\" />."));
        }
        catch (Exception exception)
        {
            OnSelectionFailed(exception);
        }
    }
}

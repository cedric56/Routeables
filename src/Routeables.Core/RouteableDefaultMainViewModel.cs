using System.Collections.ObjectModel;
using System.ComponentModel;
using Routeables.Abstraction;

namespace Routeables.Core;

/// <summary>
/// The main view model of an application that has no menu: <c>UseRouteables(r => ...)</c> without a type registers it. It lists no
/// entry; the page shown first is the one given to <see cref="Routeables.Abstraction.Extensions.AddHome"/>, if any. Write your own
/// (<see cref="MainViewModelBase"/>) when the application has a menu, a tab bar or a title to show.
/// </summary>
/// <param name="router">The router that shows the pages.</param>
/// <param name="home">The route to open at start, registered by <c>AddHome</c>; <see langword="null"/> when none was.</param>
public sealed class RouteableDefaultMainViewModel(IRouter router, RouteableHome? home = null)
    : MainViewModelBase(router, initialize: false), INotifyPropertyChanged, IMainViewModel
{
    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <inheritdoc />
    protected override void NotifyPropertyChanged(string? propertyName)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    /// <summary>No entry: the application has no menu.</summary>
    protected override ObservableCollection<IRouteableEntry> GetRouteables() => [];

    // Every host calls it once at start: with a home route, this is where the first page is chosen.
    bool IMainViewModel.SelectFirst()
    {
        if (home is null || _router.CurrentView is not null)
            return false;

        _ = StartAsync(home.Route);
        return true;
    }

    private async Task StartAsync(string route)
    {
        try
        {
            var result = await _router.GoToAsync(route);
            if (result.Success)
                OnRouterMoved(_router.CurrentViewModel);
            else
                OnSelectionFailed(result.Exception ?? new InvalidOperationException($"The home route '{route}' could not be opened."));
        }
        catch (Exception exception)
        {
            OnSelectionFailed(exception);
        }
    }
}

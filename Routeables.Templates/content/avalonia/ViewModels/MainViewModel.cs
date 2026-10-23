using Routeables.Abstraction;
using Routeables.Core;
using Routeables.Routing;
using System.Collections.ObjectModel;

namespace RouteablesApp.ViewModels;

/// <summary>The menu of the application. MainViewModelBase does the rest: the selected entry, the current view and the notifications.</summary>
public sealed class MainViewModel(IRouter router) : MainViewModelBase(router)
{
    protected override ObservableCollection<IRouteableEntry> GetRouteables() =>
    [
        new RouteableEntry<HomeViewModel>("Home", route: "/home", template: "/home"),
    ];
}

using Routeables.Abstraction;
using Routeables.Core;
using Routeables.Routing;
using System.Collections.ObjectModel;
using Routeables.Segments;
using XAnimals.Common;

namespace XAnimals.ViewModels;

/// <summary>
/// The menu of the sample. <see cref="MainViewModelBase"/> does the rest: the selected entry (with the refusal of a page that
/// cannot be left), the current view, the lock coordinator and the notifications. The hosts select the first entry themselves.
/// </summary>
public sealed class MainViewModel(IRouter router, IRouteCoordinator coordinator) : MainViewModelBase(router, initialize: false)
{
    protected override ObservableCollection<IRouteableEntry> GetRouteables() =>
    [
        new RouteableEntry<CatsViewModel>("Cats", AnimalIcons.Cat, route: "/cats", template: "/cats", description: string.Join(Environment.NewLine, "GoToRouteAsync<TViewModel>", "Named values (WithValue)")),
        new RouteableEntry<DogsViewModel>("Dogs", AnimalIcons.Dog, route: "/dogs", template: "/dogs", description: string.Join(Environment.NewLine, "GoToRouteAsync(view)", "Arguments for the view model (ForViewModel)")),
        new RouteableEntry<MonkeysViewModel>("Monkeys", AnimalIcons.Monkey, route: "/monkeys", template: "/monkeys", description: string.Join(Environment.NewLine, "Generated GoToMonkeyDetailAsync", "BackAsync to return")),
        new RouteableEntry<ElephantsViewModel>("Elephants", AnimalIcons.Elephant, route: "/elephants", template: "/elephants", description: string.Join(Environment.NewLine, "Generated GoToElephantDetailAsync","PopAsync to return")),
        new RouteableEntry<BearsViewModel>("Bears", AnimalIcons.Bear, route: "/bears", template: "/bears", description: string.Join(Environment.NewLine, "GoToRouteAsync<TViewModel, TArgument>", "One typed argument")),
        new RouteableEntry<PandasViewModel>("Pandas", AnimalIcons.Panda, route: "/pandas", template: "/pandas", description: string.Join(Environment.NewLine, "GoToRouteAsync(view)", "Parameter read from the address by attribute")),
        new RouteableEntry<HistoryViewModel>("History", AnimalIcons.History, route: "history", template: "history", description: string.Join(Environment.NewLine, "BackAsync, ForwardAsync, PopAsync", "CanLeaveAsync can refuse")),
        new RouteableEntry<RegionsViewModel>("Regions", AnimalIcons.Regions, route: "regions", template: "regions", description: string.Join(Environment.NewLine, "RouteableRegion", "Header, Footer, LeftPanel, Content", "An entry names its region")),
        new RouteableEntry<LockViewModel>("Lock", AnimalIcons.Lock, route: "lock", template: "lock", description: string.Join(Environment.NewLine, "ILockCoordinator", "A RouteableLockMessage locks the menu")),
        new RouteableEntry<DialogsViewModel>("Dialogs", AnimalIcons.Dialogs, route: "dialogs", template: "dialogs", description: string.Join(Environment.NewLine, "Dialogs, notifications and modal", "The services next to IRouter")),
        new RouteableEntry<AboutViewModel>("About", AnimalIcons.About, route: "about", template: "about", description: string.Join(Environment.NewLine, "The logo", "A link and a segment route"))
    ];

    /// <summary>
    /// Navigation by segments (see <see cref="RouteCoordination"/>): <c>/Bears?SearchText=polar</c> opens the bears list
    /// with its search filled in; a leading <c>/</c> clears the history first, <c>..</c> goes back one entry.
    /// The segment names are registered in <c>RegisterViewModels</c>.
    /// </summary>
    public async Task<RouteableResult> GoToAsync(string uri)
    {
        var result = await coordinator.CoordinateAsync(uri);
        if (result.Success)
            CurrentView = _router.CurrentView;
        return result;
    }
}

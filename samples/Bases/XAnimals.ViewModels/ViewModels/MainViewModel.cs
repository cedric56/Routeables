using Routeables.Abstraction;
using Routeables.Core;
using Routeables.Routing;
using Routeables.Segments;
using Routeables.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using XAnimals.Common;

namespace XAnimals.ViewModels;

/// <summary>
/// The menu of the sample. <see cref="MainViewModelBase"/> does the rest: the selected entry (with the refusal of a page that
/// cannot be left), the current view, the lock coordinator and the notifications. The hosts select the first entry themselves.
/// </summary>
[System.ComponentModel.Bindable(true)]
public class MainViewModel(IRouter router, IRouteCoordinator coordinator, IRouteableRequestedThemeService themes) : MainViewModelBase(router, initialize: false), INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// The button of the app bar: switches to the theme that is not the one shown. The host knows which one it shows (the system
    /// theme when none was asked), so it passes it; the view model asks for the other through <see cref="IRouteableRequestedThemeService"/>.
    /// </summary>
    public void ToggleTheme(bool isDark) => themes.Request(isDark ? RouteableRequestedTheme.Light : RouteableRequestedTheme.Dark);

    protected override ObservableCollection<IRouteableMenuEntry> GetEntries() =>
    [
        new RouteableMenuEntry<CatsViewModel>("Cats", AnimalIcons.Cat, route: "/cats", template: "/cats", description: string.Join(Environment.NewLine, "GoToRouteAsync<TViewModel>", "Named values (WithValue)")),
        new RouteableMenuEntry<DogsViewModel>("Dogs", AnimalIcons.Dog, route: "/dogs", template: "/dogs", description: string.Join(Environment.NewLine, "GoToRouteAsync(view)", "Arguments for the view model (ForViewModel)")),
        new RouteableMenuEntry<MonkeysViewModel>("Monkeys", AnimalIcons.Monkey, route: "/monkeys", template: "/monkeys", description: string.Join(Environment.NewLine, "Generated GoToMonkeyDetailAsync", "BackAsync to return")),
        new RouteableMenuEntry<ElephantsViewModel>("Elephants", AnimalIcons.Elephant, route: "/elephants", template: "/elephants", description: string.Join(Environment.NewLine, "Generated GoToElephantDetailAsync","PopAsync to return")),
        new RouteableMenuEntry<BearsViewModel>("Bears", AnimalIcons.Bear, route: "/bears", template: "/bears", description: string.Join(Environment.NewLine, "GoToRouteAsync<TViewModel, TArgument>", "One typed argument")),
        new RouteableMenuEntry<PandasViewModel>("Pandas", AnimalIcons.Panda, route: "/pandas", template: "/pandas", description: string.Join(Environment.NewLine, "GoToAsync<TViewModel>(values)", "The route is built from the registered template")),
        new RouteableMenuEntry<HistoryViewModel>("History", AnimalIcons.History, route: "history", template: "history", description: string.Join(Environment.NewLine, "BackAsync, ForwardAsync, PopAsync", "CanLeaveAsync can refuse")),
        new RouteableMenuEntry<RegionsViewModel>("Regions", AnimalIcons.Regions, route: "regions", template: "regions", description: string.Join(Environment.NewLine, "RouteableRegion", "Header, Footer, LeftPanel, Content", "An entry names its region")),
        new RouteableMenuEntry<AdoptViewModel>("Adopt a pet", AnimalIcons.Regions, route: "adopt", template: "adopt", description: string.Join(Environment.NewLine, "RouteableRegionItems", "Every page of a region, in rows and columns", "PopAsync releases the last")),
        new RouteableMenuEntry<DialogsViewModel>("Dialogs", AnimalIcons.Dialogs, route: "dialogs", template: "dialogs", description: string.Join(Environment.NewLine, "Dialogs, notifications and modal", "The services next to IRouter")),
        new RouteableMenuEntry<FeaturesViewModel>("Features", AnimalIcons.Lock, route: "features", template: "features", description: string.Join(Environment.NewLine, "IRouteGuard", "Links, saved values, lock and measures")),
        new RouteableMenuEntry<MembersViewModel>("Members", AnimalIcons.Lock, route: "members", template: "members", description: string.Join(Environment.NewLine, "IRouteGuard", "Only for members; signed out, you are sent to Features")),
        new RouteableMenuEntry<AboutViewModel>("About", AnimalIcons.About, route: "about", template: "about", description: string.Join(Environment.NewLine, "The logo", "A link and a segment route"))
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
        {
            // As after Back and Forward: the entry of the page that is now current becomes the selected one, so the menu and the
            // title follow (/Bears?... selects Bears; a detail page has no entry of its own and leaves the selection where it is).
            OnViewModelChanged(_router.CurrentViewModel);
        }
        return result;
    }

    public override async Task OnRouteableChangedAsync(IRouteableMenuEntry? value, IRouteableMenuEntry? old, int version = 0)
    {
        await base.OnRouteableChangedAsync(value, old, version);

        // A route guard may have sent the user to another page (Elephants while signed out leads to Features): the menu follows the page shown.
        if (value?.Route != "features" && _router.CurrentViewModel is FeaturesViewModel)
        {
            OnViewModelChanged(_router.CurrentViewModel);
        }
    }

    protected override void NotifyPropertyChanged(string? propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Routing;
using Routeables.Routing;
using Routeables.Segments;
using Routeables.Services;
using System.Collections.ObjectModel;
using XAnimals.Common;

namespace XAnimals.ViewModels;

/// <summary>
/// The menu of the heads that have a control of navigation to declare in XAML (<c>TabControl</c> on WPF and Avalonia,
/// <c>NavigationView</c> on Uno): the entries of <see cref="MainViewModel"/>, and the Tabs page. The other heads keep
/// <see cref="MainViewModel"/>, which has no entry for a page they cannot show.
/// </summary>
[System.ComponentModel.Bindable(true)]
public sealed class MainViewModelWithTabs(IRouter router, IRouteCoordinator coordinator, IRouteableRequestedThemeService themes) : MainViewModel(router, coordinator, themes)
{
    protected override ObservableCollection<IRouteableMenuEntry> GetEntries()
    {
        var entries = base.GetEntries();
        entries.Add(new RouteableMenuEntry<TabsViewModel>("Tabs", AnimalIcons.Regions, route: "tabs", template: "tabs", description: string.Join(Environment.NewLine, "AttachRouteables", "The items of a TabControl or NavigationView are routes")));
        return entries;
    }
}

using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Routing;
using Routeables.Core;
using Routeables.Routing;
using System.Collections.ObjectModel;
using System.ComponentModel;
using XAnimals.ViewModels;

namespace XAnimals.Wpf;

/// <summary>
/// The menu of the WPF sample: the pages that have a WPF view. The view models are those of the other samples;
/// <see cref="RouteableMenuViewModelBase"/> does the rest (selected entry, current view, lock, notifications).
/// </summary>
public sealed class MenuViewModel(IRouter router) : RouteableMenuViewModelBase(router, initialize: false), INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected override ObservableCollection<IRouteableMenuEntry> GetEntries() =>
    [
        new RouteableMenuEntry<AdoptViewModel>("Adopt a pet", null, route: "adopt", template: "adopt", description: string.Join(Environment.NewLine, "RouteableRegionItems", "Every page of a region, in rows and columns", "PopAsync releases the last")),
        new RouteableMenuEntry<FeaturesViewModel>("Features", null, route: "features", template: "features", description: string.Join(Environment.NewLine, "IRouteGuard", "Links, saved values, lock and measures")),
        new RouteableMenuEntry<DialogsViewModel>("Dialogs", null, route: "dialogs", template: "dialogs", description: string.Join(Environment.NewLine, "Dialogs, notifications and modal", "The services next to IRouter"))
    ];

    protected override void NotifyPropertyChanged(string? propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

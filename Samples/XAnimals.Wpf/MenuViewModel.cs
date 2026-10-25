using System.Collections.ObjectModel;
using System.ComponentModel;
using Routeables.Abstraction;
using Routeables.Core;
using Routeables.Routing;
using XAnimals.ViewModels;

namespace XAnimals.Wpf;

/// <summary>
/// The menu of the WPF sample: the pages that have a WPF view. The view models are those of the other samples;
/// <see cref="MainViewModelBase"/> does the rest (selected entry, current view, lock, notifications).
/// </summary>
public sealed class MenuViewModel(IRouter router) : MainViewModelBase(router, initialize: false), INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected override ObservableCollection<IRouteableEntry> GetRouteables() =>
    [
        new RouteableEntry<AdoptViewModel>("Adopt a pet", null, route: "adopt", template: "adopt", description: string.Join(Environment.NewLine, "RouteableRegionItems", "Every page of a region, in rows and columns", "PopAsync releases the last")),
        new RouteableEntry<FeaturesViewModel>("Features", null, route: "features", template: "features", description: string.Join(Environment.NewLine, "IRouteGuard", "Links, saved values, lock and measures")),
        new RouteableEntry<DialogsViewModel>("Dialogs", null, route: "dialogs", template: "dialogs", description: string.Join(Environment.NewLine, "Dialogs, notifications and modal", "The services next to IRouter"))
    ];

    protected override void NotifyPropertyChanged(string? propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

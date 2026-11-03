using Microsoft.UI.Xaml;
using Routeables.Uno;
using XAnimals.ViewModels;

namespace XAnimals.Uno.Views;

public sealed partial class TabsView : RouteableFor
{
    private IDisposable? _navigation;

    public TabsView()
    {
        InitializeComponent();
        Loaded += (_, _) => _navigation ??= Navigation.AttachRouteables(Application.Current.GetRouteableServices(), TabsViewModel.Region);
        Unloaded += (_, _) => { _navigation?.Dispose(); _navigation = null; };
    }
}

using Microsoft.UI.Xaml;
using Routeables.WinUI3;
using XAnimals.ViewModels;

namespace XAnimals.WinUI3.Views;

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

using System.Windows;
using Routeables.Wpf;
using XAnimals.ViewModels;

namespace XAnimals.Wpf.Views;

public partial class TabsView : RouteableFor
{
    private IDisposable? _tabs;

    public TabsView()
    {
        InitializeComponent();
        Loaded += (_, _) => _tabs ??= Tabs.AttachRouteables(Application.Current.GetRouteableServices(), TabsViewModel.Region);
        Unloaded += (_, _) => { _tabs?.Dispose(); _tabs = null; };
    }
}

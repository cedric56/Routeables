using Routeables.Abstraction;
using Routeables.Avalonia;
using XAnimals.ViewModels;

namespace XAnimals.Avalonia;

[RouteableView(typeof(LayoutBarViewModel), route: "layout-bar")]
public partial class LayoutBarView : RouteableFor<LayoutBarViewModel>
{
    public LayoutBarView()
    {
        InitializeComponent();
    }
}

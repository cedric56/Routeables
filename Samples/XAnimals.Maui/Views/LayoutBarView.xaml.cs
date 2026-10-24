using Routeables.Abstraction;
using Routeables.Maui;
using XAnimals.ViewModels;

namespace XAnimals.Maui.Views;

[RouteableView(typeof(LayoutBarViewModel), route: "layout-bar")]
public partial class LayoutBarView : RouteableFor
{
    public LayoutBarView()
    {
        InitializeComponent();
    }
}

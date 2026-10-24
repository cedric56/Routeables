using Routeables.Abstraction;
using Routeables.Maui;
using XAnimals.ViewModels;

namespace XAnimals.Maui.Views;

[RouteableView(typeof(LayoutMenuViewModel), route: "layout-menu")]
public partial class LayoutMenuView : RouteableFor
{
    public LayoutMenuView()
    {
        InitializeComponent();
    }
}

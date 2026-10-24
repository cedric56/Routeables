using Routeables.Abstraction;
using Routeables.Avalonia;
using XAnimals.ViewModels;

namespace XAnimals.Avalonia;

[RouteableView(typeof(LayoutMenuViewModel), route: "layout-menu")]
public partial class LayoutMenuView : RouteableFor<LayoutMenuViewModel>
{
    public LayoutMenuView()
    {
        InitializeComponent();
    }
}

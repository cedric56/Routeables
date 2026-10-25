using Routeables.Abstraction;
using Routeables.Avalonia;
using XAnimals.ViewModels;

namespace XAnimals.Avalonia;

public partial class LayoutMenuView : RouteableFor<LayoutMenuViewModel>
{
    public LayoutMenuView()
    {
        InitializeComponent();
    }
}

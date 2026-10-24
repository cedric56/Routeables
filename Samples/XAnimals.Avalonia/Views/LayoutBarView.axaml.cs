using Routeables.Abstraction;
using Routeables.Avalonia;
using XAnimals.ViewModels;

namespace XAnimals.Avalonia;

public partial class LayoutBarView : RouteableFor<LayoutBarViewModel>
{
    public LayoutBarView()
    {
        InitializeComponent();
    }
}

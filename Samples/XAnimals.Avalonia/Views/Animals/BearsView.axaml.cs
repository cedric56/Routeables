using Routeables.Abstraction;
using Routeables.Avalonia;
using XAnimals.ViewModels;

namespace XAnimals.Avalonia;

public partial class BearsView : RouteableFor<BearsViewModel>
{
    public BearsView()
    {
        InitializeComponent();
    }
}

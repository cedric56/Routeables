using Routeables.Abstraction;
using Routeables.Avalonia;
using XAnimals.Birds;

namespace XAnimals.Avalonia;

[RouteableView(typeof(BirdsViewModel), route: BirdsModule.Route)]
public partial class BirdsView : RouteableFor<BirdsViewModel>
{
    public BirdsView()
    {
        InitializeComponent();
    }
}

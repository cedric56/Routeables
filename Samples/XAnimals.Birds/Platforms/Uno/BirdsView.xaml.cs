using Routeables.Abstraction;
using Routeables.Uno;
using XAnimals.Birds;

namespace XAnimals.Uno.Views;

[RouteableView(typeof(BirdsViewModel), route: BirdsModule.Route)]
public sealed partial class BirdsView : RouteableFor
{
    public BirdsView()
    {
        InitializeComponent();
    }
}

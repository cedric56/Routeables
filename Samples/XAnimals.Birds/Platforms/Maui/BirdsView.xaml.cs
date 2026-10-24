using Routeables.Abstraction;
using Routeables.Maui;
using XAnimals.Birds;

namespace XAnimals.Maui.Views;

[RouteableView(typeof(BirdsViewModel), route: BirdsModule.Route)]
public partial class BirdsView : RouteableFor
{
    public BirdsView()
    {
        InitializeComponent();
    }
}

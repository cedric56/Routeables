using Routeables.Abstraction;
using Routeables.Avalonia;
using XAnimals.ViewModels;

namespace XAnimals.Avalonia;

[RouteableView(typeof(CatsViewModel), route: "/cats")]
public partial class CatsView : RouteableFor<CatsViewModel>
{
    public CatsView()
    {
        InitializeComponent();
    }
}

using Routeables.Abstraction;
using Routeables.Avalonia;
using XAnimals.ViewModels;

namespace XAnimals.Avalonia;

[RouteableView(typeof(BearsViewModel), route: "/bears")]
public partial class BearsView : RouteableFor<BearsViewModel>
{
    public BearsView()
    {
        InitializeComponent();
    }
}

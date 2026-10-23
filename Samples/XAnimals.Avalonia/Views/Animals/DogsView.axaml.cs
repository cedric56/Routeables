using Routeables.Abstraction;
using Routeables.Avalonia;
using XAnimals.ViewModels;

namespace XAnimals.Avalonia;

[RouteableView(typeof(DogsViewModel), route: "/dogs")]
public partial class DogsView : RouteableFor<DogsViewModel>
{
    public DogsView()
    {
        InitializeComponent();
    }
}

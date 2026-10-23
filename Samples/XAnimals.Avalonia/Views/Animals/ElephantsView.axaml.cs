using Routeables.Abstraction;
using Routeables.Avalonia;
using XAnimals.ViewModels;

namespace XAnimals.Avalonia;

[RouteableView(typeof(ElephantsViewModel), route: "/elephants")]
public partial class ElephantsView : RouteableFor<ElephantsViewModel>
{
    public ElephantsView()
    {
        InitializeComponent();
    }
}

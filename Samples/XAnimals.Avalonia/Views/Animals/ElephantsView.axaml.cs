using Routeables.Abstraction;
using Routeables.Avalonia;
using XAnimals.ViewModels;

namespace XAnimals.Avalonia;

public partial class ElephantsView : RouteableFor<ElephantsViewModel>
{
    public ElephantsView()
    {
        InitializeComponent();
    }
}

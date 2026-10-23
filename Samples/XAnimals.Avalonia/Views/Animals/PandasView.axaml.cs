using Routeables.Abstraction;
using Routeables.Avalonia;
using XAnimals.ViewModels;

namespace XAnimals.Avalonia;

[RouteableView(typeof(PandasViewModel), route: "/pandas")]
public partial class PandasView : RouteableFor<PandasViewModel>
{
    public PandasView()
    {
        InitializeComponent();
    }
}

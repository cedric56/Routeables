using Routeables.Abstraction;
using Routeables.Avalonia;
using XAnimals.ViewModels;

namespace XAnimals.Avalonia;

public partial class PandasView : RouteableFor<PandasViewModel>
{
    public PandasView()
    {
        InitializeComponent();
    }
}

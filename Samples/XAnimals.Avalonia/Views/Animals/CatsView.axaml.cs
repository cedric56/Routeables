using Routeables.Abstraction;
using Routeables.Avalonia;
using XAnimals.ViewModels;

namespace XAnimals.Avalonia;

public partial class CatsView : RouteableFor<CatsViewModel>
{
    public CatsView()
    {
        InitializeComponent();
    }
}

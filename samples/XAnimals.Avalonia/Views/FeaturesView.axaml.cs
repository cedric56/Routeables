using Routeables.Abstraction;
using Routeables.Avalonia;
using XAnimals.ViewModels;

namespace XAnimals.Avalonia;

public partial class FeaturesView : RouteableFor<FeaturesViewModel>
{
    public FeaturesView()
    {
        InitializeComponent();
    }
}

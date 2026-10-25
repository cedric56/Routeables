using Routeables.Avalonia;
using XAnimals.ViewModels;

namespace XAnimals.Avalonia;

public partial class AdoptView : RouteableFor<AdoptViewModel>
{
    public AdoptView()
    {
        InitializeComponent();
    }
}

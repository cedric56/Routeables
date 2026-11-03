using Routeables.Avalonia;
using XAnimals.ViewModels;

namespace XAnimals.Avalonia;

public partial class PetView : RouteableFor<PetViewModel>
{
    public PetView()
    {
        InitializeComponent();
    }
}

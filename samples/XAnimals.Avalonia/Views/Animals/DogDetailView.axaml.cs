using Routeables.Abstraction;
using Routeables.Avalonia;
using XAnimals.ViewModels;

namespace XAnimals.Avalonia;

public partial class DogDetailView : RouteableFor<DogDetailViewModel>
{
    public DogDetailView()
    {
        InitializeComponent();
    }
}

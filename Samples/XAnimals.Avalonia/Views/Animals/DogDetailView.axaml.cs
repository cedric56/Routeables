using Routeables.Abstraction;
using Routeables.Avalonia;
using XAnimals.ViewModels;

namespace XAnimals.Avalonia;

[RouteableView(typeof(DogDetailViewModel), route: "/dogs/{name}")]
public partial class DogDetailView : RouteableFor<DogDetailViewModel>
{
    public DogDetailView()
    {
        InitializeComponent();
    }
}

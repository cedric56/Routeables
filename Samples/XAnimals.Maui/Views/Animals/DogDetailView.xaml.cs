using Routeables.Abstraction;
using Routeables.Maui;
using XAnimals.ViewModels;

namespace XAnimals.Maui.Views;

[RouteableView(typeof(DogDetailViewModel), route: "/dogs/{name}")]
public partial class DogDetailView : RouteableFor
{
    public DogDetailView()
    {
        InitializeComponent();
    }
}

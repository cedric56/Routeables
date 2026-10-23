using Routeables.Abstraction;
using Routeables.Maui;
using XAnimals.ViewModels;

namespace XAnimals.Maui.Views;

[RouteableView(typeof(DogsViewModel), route: "/dogs")]
public partial class DogsView : RouteableFor
{
    public DogsView()
    {
        InitializeComponent();
    }
}

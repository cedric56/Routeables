using Routeables.Abstraction;
using Routeables.Maui;
using XAnimals.ViewModels;

namespace XAnimals.Maui.Views;

[RouteableView(typeof(BearsViewModel), route: "/bears")]
public partial class BearsView : RouteableFor
{
    public BearsView()
    {
        InitializeComponent();
    }
}

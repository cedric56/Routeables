using Routeables.Abstraction;
using Routeables.Maui;
using XAnimals.ViewModels;

namespace XAnimals.Maui.Views;

[RouteableView(typeof(CatsViewModel), route: "/cats")]
public partial class CatsView : RouteableFor
{
    public CatsView()
    {
        InitializeComponent();
    }
}

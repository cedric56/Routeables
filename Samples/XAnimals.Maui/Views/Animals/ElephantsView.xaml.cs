using Routeables.Abstraction;
using Routeables.Maui;
using XAnimals.ViewModels;

namespace XAnimals.Maui.Views;

[RouteableView(typeof(ElephantsViewModel), route: "/elephants")]
public partial class ElephantsView : RouteableFor
{
    public ElephantsView()
    {
        InitializeComponent();
    }
}

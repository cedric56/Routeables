using Routeables.Abstraction;
using Routeables.Maui;
using XAnimals.ViewModels;

namespace XAnimals.Maui.Views;

[RouteableView(typeof(PandasViewModel), route: "/pandas")]
public partial class PandasView : RouteableFor
{
    public PandasView()
    {
        InitializeComponent();
    }
}

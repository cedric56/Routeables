using Routeables.Abstraction;
using Routeables.Maui;
using XAnimals.ViewModels;

namespace XAnimals.Maui.Views;

[RouteableView(typeof(BearDetailViewModel), route: "/bears/{name}")]
public partial class BearDetailView : RouteableFor
{
    public BearDetailView()
    {
        InitializeComponent();
    }
}

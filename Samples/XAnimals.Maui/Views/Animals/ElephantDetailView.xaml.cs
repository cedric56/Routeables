using Routeables.Abstraction;
using Routeables.Maui;
using XAnimals.ViewModels;

namespace XAnimals.Maui.Views;

[RouteableView(typeof(ElephantDetailViewModel), route: "/elephants/{name}")]
public partial class ElephantDetailView : RouteableFor
{
    public ElephantDetailView()
    {
        InitializeComponent();
    }
}

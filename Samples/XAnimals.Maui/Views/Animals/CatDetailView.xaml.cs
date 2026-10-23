using Routeables.Abstraction;
using Routeables.Maui;
using XAnimals.ViewModels;

namespace XAnimals.Maui.Views;

[RouteableView(typeof(CatDetailViewModel), route: "/cats/{name}")]
public partial class CatDetailView : RouteableFor
{
    public CatDetailView()
    {
        InitializeComponent();
    }
}

using Routeables.Abstraction;
using Routeables.Avalonia;
using XAnimals.ViewModels;

namespace XAnimals.Avalonia;

[RouteableView(typeof(CatDetailViewModel), route: "/cats/{name}")]
public partial class CatDetailView : RouteableFor<CatDetailViewModel>
{
    public CatDetailView()
    {
        InitializeComponent();
    }
}

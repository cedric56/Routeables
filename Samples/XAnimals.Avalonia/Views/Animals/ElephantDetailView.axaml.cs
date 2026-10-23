using Routeables.Abstraction;
using Routeables.Avalonia;
using XAnimals.ViewModels;

namespace XAnimals.Avalonia;

[RouteableView(typeof(ElephantDetailViewModel), route: "/elephants/{name}")]
public partial class ElephantDetailView : RouteableFor<ElephantDetailViewModel>
{
    public ElephantDetailView()
    {
        InitializeComponent();
    }
}

using Routeables.Abstraction;
using Routeables.Avalonia;
using XAnimals.ViewModels;

namespace XAnimals.Avalonia;

[RouteableView(typeof(BearDetailViewModel), route: "/bears/{name}")]
public partial class BearDetailView : RouteableFor<BearDetailViewModel>
{
    public BearDetailView()
    {
        InitializeComponent();
    }
}

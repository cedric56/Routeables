using Routeables.Abstraction;
using Routeables.Avalonia;
using XAnimals.ViewModels;

namespace XAnimals.Avalonia;

public partial class CatDetailView : RouteableFor<CatDetailViewModel>
{
    public CatDetailView()
    {
        InitializeComponent();
    }
}

using Routeables.Abstraction;
using Routeables.Avalonia;
using XAnimals.ViewModels;

namespace XAnimals.Avalonia;

public partial class BearDetailView : RouteableFor<BearDetailViewModel>
{
    public BearDetailView()
    {
        InitializeComponent();
    }
}

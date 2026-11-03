using Routeables.Abstraction;
using Routeables.Avalonia;
using XAnimals.ViewModels;

namespace XAnimals.Avalonia;

public partial class DogsView : RouteableFor<DogsViewModel>
{
    public DogsView()
    {
        InitializeComponent();
    }
}

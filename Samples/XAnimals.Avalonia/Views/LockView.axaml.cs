using Routeables.Abstraction;
using Routeables.Avalonia;
using XAnimals.ViewModels;

namespace XAnimals.Avalonia;

public partial class LockView : RouteableFor<LockViewModel>
{
    public LockView()
    {
        InitializeComponent();
    }
}

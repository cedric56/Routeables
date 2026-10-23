using Routeables.Abstraction;
using Routeables.Avalonia;
using XAnimals.ViewModels;

namespace XAnimals.Avalonia;

[RouteableView(typeof(LockViewModel), route: "lock")]
public partial class LockView : RouteableFor<LockViewModel>
{
    public LockView()
    {
        InitializeComponent();
    }
}

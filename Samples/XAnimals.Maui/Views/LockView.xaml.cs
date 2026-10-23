using Routeables.Abstraction;
using Routeables.Maui;
using XAnimals.ViewModels;

namespace XAnimals.Maui.Views;

[RouteableView(typeof(LockViewModel), route: "lock")]
public partial class LockView : RouteableFor
{
    public LockView()
    {
        InitializeComponent();
    }
}

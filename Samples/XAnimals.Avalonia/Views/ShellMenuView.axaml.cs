using Routeables.Abstraction;
using Routeables.Avalonia;
using XAnimals.ViewModels;

namespace XAnimals.Avalonia;

[RouteableView(typeof(ShellMenuViewModel), route: "shell-menu")]
public partial class ShellMenuView : RouteableFor<ShellMenuViewModel>
{
    public ShellMenuView()
    {
        InitializeComponent();
    }
}

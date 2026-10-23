using Routeables.Abstraction;
using Routeables.Maui;
using XAnimals.ViewModels;

namespace XAnimals.Maui.Views;

[RouteableView(typeof(ShellMenuViewModel), route: "shell-menu")]
public partial class ShellMenuView : RouteableFor
{
    public ShellMenuView()
    {
        InitializeComponent();
    }
}

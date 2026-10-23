using Routeables.Abstraction;
using Routeables.Avalonia;
using XAnimals.ViewModels;

namespace XAnimals.Avalonia;

[RouteableView(typeof(ShellBarViewModel), route: "shell-bar")]
public partial class ShellBarView : RouteableFor<ShellBarViewModel>
{
    public ShellBarView()
    {
        InitializeComponent();
    }
}

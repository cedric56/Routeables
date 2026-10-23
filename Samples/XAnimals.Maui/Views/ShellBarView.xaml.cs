using Routeables.Abstraction;
using Routeables.Maui;
using XAnimals.ViewModels;

namespace XAnimals.Maui.Views;

[RouteableView(typeof(ShellBarViewModel), route: "shell-bar")]
public partial class ShellBarView : RouteableFor
{
    public ShellBarView()
    {
        InitializeComponent();
    }
}

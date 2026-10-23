using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Routeables.Abstraction;
using Routeables.Uno;
using XAnimals.ViewModels;

namespace XAnimals.Uno.Views;

[RouteableView(typeof(ShellMenuViewModel), route: "shell-menu")]
public sealed partial class ShellMenuView : RouteableFor
{
    public ShellMenuView()
    {
        InitializeComponent();
    }
}

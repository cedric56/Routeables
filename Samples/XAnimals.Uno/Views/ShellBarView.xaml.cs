using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Routeables.Abstraction;
using Routeables.Uno;
using XAnimals.ViewModels;

namespace XAnimals.Uno.Views;

[RouteableView(typeof(ShellBarViewModel), route: "shell-bar")]
public sealed partial class ShellBarView : RouteableFor
{
    public ShellBarView()
    {
        InitializeComponent();
    }
}

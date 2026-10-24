using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Routeables.Abstraction;
using Routeables.Uno;
using XAnimals.ViewModels;

namespace XAnimals.Uno.Views;

[RouteableView(typeof(LayoutBarViewModel), route: "layout-bar")]
public sealed partial class LayoutBarView : RouteableFor
{
    public LayoutBarView()
    {
        InitializeComponent();
    }
}

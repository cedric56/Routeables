using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Routeables.Abstraction;
using Routeables.Uno;
using XAnimals.ViewModels;

namespace XAnimals.Uno.Views;

[RouteableView(typeof(LayoutMenuViewModel), route: "layout-menu")]
public sealed partial class LayoutMenuView : RouteableFor
{
    public LayoutMenuView()
    {
        InitializeComponent();
    }
}

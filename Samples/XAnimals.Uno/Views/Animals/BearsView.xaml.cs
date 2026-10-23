using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Routeables.Abstraction;
using Routeables.Uno;
using XAnimals.ViewModels;

namespace XAnimals.Uno.Views;

[RouteableView(typeof(BearsViewModel), route: "/bears")]
public sealed partial class BearsView : RouteableFor
{
    public BearsView()
    {
        InitializeComponent();
    }
}

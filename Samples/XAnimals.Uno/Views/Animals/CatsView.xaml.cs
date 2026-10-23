using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Routeables.Abstraction;
using Routeables.Uno;
using XAnimals.ViewModels;

namespace XAnimals.Uno.Views;

[RouteableView(typeof(CatsViewModel), route: "/cats")]
public sealed partial class CatsView : RouteableFor
{
    public CatsView()
    {
        InitializeComponent();
    }
}

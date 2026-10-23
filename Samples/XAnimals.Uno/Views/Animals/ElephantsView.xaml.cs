using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Routeables.Abstraction;
using Routeables.Uno;
using XAnimals.ViewModels;

namespace XAnimals.Uno.Views;

[RouteableView(typeof(ElephantsViewModel), route: "/elephants")]
public sealed partial class ElephantsView : RouteableFor
{
    public ElephantsView()
    {
        InitializeComponent();
    }
}

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Routeables.Abstraction;
using Routeables.Uno;
using XAnimals.ViewModels;

namespace XAnimals.Uno.Views;

[RouteableView(typeof(DogsViewModel), route: "/dogs")]
public sealed partial class DogsView : RouteableFor
{
    public DogsView()
    {
        InitializeComponent();
    }
}

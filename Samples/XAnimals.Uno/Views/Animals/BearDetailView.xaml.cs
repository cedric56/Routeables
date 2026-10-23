using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Routeables.Abstraction;
using Routeables.Uno;
using XAnimals.ViewModels;

namespace XAnimals.Uno.Views;

[RouteableView(typeof(BearDetailViewModel), route: "/bears/{name}")]
public sealed partial class BearDetailView : RouteableFor
{
    public BearDetailView()
    {
        InitializeComponent();
    }
}

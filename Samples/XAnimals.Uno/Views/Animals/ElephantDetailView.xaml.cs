using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Routeables.Abstraction;
using Routeables.Uno;
using XAnimals.ViewModels;

namespace XAnimals.Uno.Views;

[RouteableView(typeof(ElephantDetailViewModel), route: "/elephants/{name}")]
public sealed partial class ElephantDetailView : RouteableFor
{
    public ElephantDetailView()
    {
        InitializeComponent();
    }
}

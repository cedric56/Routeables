using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Routeables.Abstraction;
using Routeables.Uno;
using XAnimals.ViewModels;

namespace XAnimals.Uno.Views;

[RouteableView(typeof(CatDetailViewModel), route: "/cats/{name}")]
public sealed partial class CatDetailView : RouteableFor
{
    public CatDetailView()
    {
        InitializeComponent();
    }
}

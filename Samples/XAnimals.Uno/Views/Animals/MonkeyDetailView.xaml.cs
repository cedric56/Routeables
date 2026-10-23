using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Routeables.Abstraction;
using Routeables.Uno;
using XAnimals.ViewModels;
using Xaminals.ViewModels;

namespace XAnimals.Uno.Views;

[RouteableView(typeof(MonkeyDetailViewModel), route: "/monkeys/{name}")]
public sealed partial class MonkeyDetailView : RouteableFor
{
    public MonkeyDetailView()
    {
        InitializeComponent();
    }
}

using Routeables.Abstraction;
using Routeables.Maui;
using Xaminals.ViewModels;

namespace XAnimals.Maui.Views;

[RouteableView(typeof(MonkeyDetailViewModel), route: "/monkeys/{name}")]
public partial class MonkeyDetailView : RouteableFor
{
    public MonkeyDetailView()
    {
        InitializeComponent();
    }
}

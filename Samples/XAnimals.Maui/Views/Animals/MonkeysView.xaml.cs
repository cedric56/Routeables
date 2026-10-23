using Routeables.Abstraction;
using Routeables.Maui;
using XAnimals.ViewModels;

namespace XAnimals.Maui.Views;

[RouteableView(typeof(MonkeysViewModel), route: "/monkeys")]
public partial class MonkeysView : RouteableFor
{
    public MonkeysView()
    {
        InitializeComponent();
    }
}

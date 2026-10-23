using Routeables.Abstraction;
using Routeables.Avalonia;
using XAnimals.ViewModels;

namespace XAnimals.Avalonia;

[RouteableView(typeof(MonkeysViewModel), route: "/monkeys")]
public partial class MonkeysView : RouteableFor<MonkeysViewModel>
{
    public MonkeysView()
    {
        InitializeComponent();
    }
}
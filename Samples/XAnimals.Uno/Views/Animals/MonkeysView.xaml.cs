using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Routeables.Abstraction;
using Routeables.Uno;
using XAnimals.ViewModels;

namespace XAnimals.Uno.Views;

[RouteableView(typeof(MonkeysViewModel), route: "/monkeys")]
public sealed partial class MonkeysView : RouteableFor
{
    public MonkeysView()
    {
        InitializeComponent();
    }
}

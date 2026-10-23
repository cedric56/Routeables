using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Routeables.Abstraction;
using Routeables.Uno;
using XAnimals.ViewModels;

namespace XAnimals.Uno.Views;

[RouteableView(typeof(LockViewModel), route: "lock")]
public sealed partial class LockView : RouteableFor
{
    public LockView()
    {
        InitializeComponent();
    }
}

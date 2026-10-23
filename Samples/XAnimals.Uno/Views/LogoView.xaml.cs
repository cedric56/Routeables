using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Routeables.Abstraction;
using Routeables.Uno;
using XAnimals.ViewModels;

namespace XAnimals.Uno.Views;

[RouteableView(typeof(LogoViewModel), route: "shell-logo")]
public sealed partial class LogoView : RouteableFor
{
    public LogoView()
    {
        InitializeComponent();
    }
}

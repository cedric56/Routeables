using Routeables.Abstraction;
using Routeables.Maui;
using XAnimals.ViewModels;

namespace XAnimals.Maui.Views;

[RouteableView(typeof(LogoViewModel), route: "shell-logo")]
public partial class LogoView : RouteableFor
{
    public LogoView()
    {
        InitializeComponent();
    }
}

using Routeables.Abstraction;
using Routeables.Avalonia;
using XAnimals.ViewModels;

namespace XAnimals.Avalonia;

[RouteableView(typeof(LogoViewModel), route: "shell-logo")]
public partial class LogoView : RouteableFor<LogoViewModel>
{
    public LogoView()
    {
        InitializeComponent();
    }
}

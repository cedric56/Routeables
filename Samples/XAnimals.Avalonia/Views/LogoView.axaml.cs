using Routeables.Abstraction;
using Routeables.Avalonia;
using XAnimals.ViewModels;

namespace XAnimals.Avalonia;

public partial class LogoView : RouteableFor<LogoViewModel>
{
    public LogoView()
    {
        InitializeComponent();
    }
}

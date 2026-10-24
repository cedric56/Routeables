using Routeables.Abstraction;
using Routeables.Avalonia;
using XAnimals.ViewModels;

namespace XAnimals.Avalonia;

[RouteableView(typeof(ModulesViewModel), route: "modules")]
public partial class ModulesView : RouteableFor<ModulesViewModel>
{
    public ModulesView()
    {
        InitializeComponent();
    }
}

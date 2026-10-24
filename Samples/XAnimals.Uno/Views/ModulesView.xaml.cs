using Routeables.Abstraction;
using Routeables.Uno;
using XAnimals.ViewModels;

namespace XAnimals.Uno.Views;

[RouteableView(typeof(ModulesViewModel), route: "modules")]
public sealed partial class ModulesView : RouteableFor
{
    public ModulesView()
    {
        InitializeComponent();
    }
}

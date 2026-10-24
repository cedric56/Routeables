using Routeables.Abstraction;
using Routeables.Maui;
using XAnimals.ViewModels;

namespace XAnimals.Maui.Views;

[RouteableView(typeof(ModulesViewModel), route: "modules")]
public partial class ModulesView : RouteableFor
{
    public ModulesView()
    {
        InitializeComponent();
    }
}

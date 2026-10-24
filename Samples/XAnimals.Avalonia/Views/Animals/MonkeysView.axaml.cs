using Routeables.Abstraction;
using Routeables.Avalonia;
using XAnimals.ViewModels;

namespace XAnimals.Avalonia;

public partial class MonkeysView : RouteableFor<MonkeysViewModel>
{
    public MonkeysView()
    {
        InitializeComponent();
    }
}
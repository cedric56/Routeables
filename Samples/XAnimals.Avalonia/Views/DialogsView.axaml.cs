using Routeables.Abstraction;
using Routeables.Avalonia;
using XAnimals.ViewModels;

namespace XAnimals.Avalonia;

public partial class DialogsView : RouteableFor<DialogsViewModel>
{
    public DialogsView()
    {
        InitializeComponent();
    }
}

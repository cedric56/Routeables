using Routeables.Abstraction;
using Routeables.Avalonia;
using XAnimals.ViewModels;

namespace XAnimals.Avalonia;

[RouteableView(typeof(DialogsViewModel), route: "dialogs")]
public partial class DialogsView : RouteableFor<DialogsViewModel>
{
    public DialogsView()
    {
        InitializeComponent();
    }
}

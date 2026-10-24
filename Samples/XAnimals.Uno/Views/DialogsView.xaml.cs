using Routeables.Abstraction;
using Routeables.Uno;
using XAnimals.ViewModels;

namespace XAnimals.Uno.Views;

[RouteableView(typeof(DialogsViewModel), route: "dialogs")]
public sealed partial class DialogsView : RouteableFor
{
    public DialogsView()
    {
        InitializeComponent();
    }
}

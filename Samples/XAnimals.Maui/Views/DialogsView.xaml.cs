using Routeables.Abstraction;
using Routeables.Maui;
using XAnimals.ViewModels;

namespace XAnimals.Maui.Views;

[RouteableView(typeof(DialogsViewModel), route: "dialogs")]
public partial class DialogsView : RouteableFor
{
    public DialogsView()
    {
        InitializeComponent();
    }
}

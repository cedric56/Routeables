using Routeables.Abstraction;
using Routeables.Maui;
using XAnimals.ViewModels;

namespace XAnimals.Maui.Views;

[RouteableView(typeof(HistoryViewModel), route: "history")]
public partial class HistoryView : RouteableFor
{
    public HistoryView()
    {
        InitializeComponent();
    }
}

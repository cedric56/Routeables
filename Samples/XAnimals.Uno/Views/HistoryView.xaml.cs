using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Routeables.Abstraction;
using Routeables.Uno;
using XAnimals.ViewModels;

namespace XAnimals.Uno.Views;

[RouteableView(typeof(HistoryViewModel), route: "history")]
public sealed partial class HistoryView : RouteableFor
{
    public HistoryView()
    {
        InitializeComponent();
    }
}

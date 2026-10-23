using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Routeables.Abstraction;
using Routeables.Uno;
using XAnimals.ViewModels;

namespace XAnimals.Uno.Views;

[RouteableView(typeof(PandasViewModel), route: "/pandas")]
public sealed partial class PandasView : RouteableFor
{
    public PandasView()
    {
        InitializeComponent();
    }
}

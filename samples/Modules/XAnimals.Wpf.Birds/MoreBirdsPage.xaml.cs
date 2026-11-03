using Routeables.Wpf;

namespace XAnimals.Wpf.Views;

// No [RouteableView]: this page is registered by AddWpfMoreBirdsModule, when the module is loaded.
public partial class MoreBirdsPage : RouteableFor
{
    public MoreBirdsPage()
    {
        InitializeComponent();
    }
}

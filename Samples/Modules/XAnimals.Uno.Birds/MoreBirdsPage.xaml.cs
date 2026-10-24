using Routeables.Uno;

namespace XAnimals.Uno.Views;

// No [RouteableView]: this page is registered by AddUnoMoreBirdsModule, when the module is loaded.
public sealed partial class MoreBirdsPage : RouteableFor
{
    public MoreBirdsPage()
    {
        InitializeComponent();
    }
}

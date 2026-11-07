using Routeables.WinUI3;

namespace XAnimals.WinUI3.Views;

// No [RouteableView]: this page is registered by AddWinUI3MoreBirdsModule, when the module is loaded.
public sealed partial class MoreBirdsPage : RouteableFor
{
    public MoreBirdsPage()
    {
        InitializeComponent();
    }
}

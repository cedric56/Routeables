using Routeables.Maui;

namespace XAnimals.Maui.Views;

// No [RouteableView]: this page is registered by AddMauiMoreBirdsModule, when the module is loaded.
public partial class MoreBirdsPage : RouteableFor
{
    public MoreBirdsPage()
    {
        InitializeComponent();
    }
}

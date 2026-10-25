using Routeables.Avalonia;
using XAnimals.Birds;

namespace XAnimals.Avalonia;

// No [RouteableView]: this page is registered by AddAvaloniaMoreBirdsModule, when the module is loaded.
public partial class MoreBirdsPage : RouteableFor<MoreBirdsViewModel>
{
    public MoreBirdsPage()
    {
        InitializeComponent();
    }
}

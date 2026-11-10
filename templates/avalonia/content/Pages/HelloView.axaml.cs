using Routeables.Avalonia;

namespace RouteablesApp;

public partial class HelloView : RouteableFor<HelloViewModel>
{
    public HelloView() => InitializeComponent();
}

using Microsoft.UI.Xaml;
using Routeables.WinUI3;

namespace RouteablesApp;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        // The container is built now; the window is opened by OnLaunched.
        // The pages are registered for you (the generator finds [Routeable] and the views). The first page is the one the
        // RouteableRegion of MainPage.xaml opens. To register by hand too, use the block: services => services.RegisterRoute<...>(...).
        Services = this.UseRouteables();
    }

    public IServiceProvider Services { get; }

    protected override void OnLaunched(LaunchActivatedEventArgs args) =>
        this.OpenMainWindow(new MainPage(), window => window.Title = "RouteablesApp");
}

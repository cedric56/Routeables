using Routeables.Uno;

namespace RouteablesApp;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        // The container is built now and the main window is created as the application runs: there is no OnLaunched to write.
        // The pages are registered for you (the generator finds [Routeable] and the views). The first page is the one the
        // RouteableRegion of MainPage.xaml opens. To register by hand too, use the block: services => services.RegisterRoute<...>(...).
        Services = this.UseRouteables(
            _ => { },
            () => new MainPage(),
            window => window.Title = "RouteablesApp");
    }

    public IServiceProvider Services { get; }
}

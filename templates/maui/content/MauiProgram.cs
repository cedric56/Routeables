using Routeables.Maui;

namespace RouteablesApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            // The pages are registered for you (the generator finds [Routeable] and the views). The first page is the one the
            // RouteableRegion of MainPage.xaml opens. To register by hand too, pass a block: services => services.RegisterRoute<...>(...).
            .UseRouteables();
        return builder.Build();
    }
}

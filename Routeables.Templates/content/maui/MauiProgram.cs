using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Routeables.Abstraction;
using Routeables.Generated;
using Routeables.Maui;
using Routeables.Maui.Services;
using RouteablesApp.ViewModels;

namespace RouteablesApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseRouteables<MainViewModel>(services => services.AddGeneratedRoutes())   // the router, the menu and the routes
            .UseRouteablesServices()    // optional: dialogs and notifications
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("Inter-Regular.otf", "InterRegular");
                fonts.AddFont("Inter-SemiBold.otf", "InterSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}

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
            .UseRouteables()            // the router
            .UseRouteablesServices()    // optional: dialogs and notifications
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("Inter-Regular.otf", "InterRegular");
                fonts.AddFont("Inter-SemiBold.otf", "InterSemibold");
            });

        builder.Services.AddGeneratedRoutes();                                             // the routes declared by attributes
        builder.Services.RegisterMainRouteables<MainViewModel>(ServiceLifetime.Singleton); // one menu for the application

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}

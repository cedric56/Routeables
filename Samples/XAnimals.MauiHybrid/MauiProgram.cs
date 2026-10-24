using Microsoft.Extensions.Logging;
using Reshapes.Razor.MudBlazor;
using Routeables.Razor;
using Routeables.Core;
using Routeables.Generated;
using Xanimals.Razor;
using XAnimals.ViewModels;

namespace XAnimals.MauiHybrid
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

            builder.Services.UseRouteablesHybrid<MainViewModel>();
            builder.Services.UseMudBlazor();
            builder.Services.AddGeneratedRoutes();   // here, not in XAnimals.Razor: the Razor views of that library are seen as referenced assemblies
            builder.Services.UseSharedLibrary();
            builder.Services.AddMauiBlazorWebView();

#if DEBUG
    		builder.Services.AddBlazorWebViewDeveloperTools();
    		builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}

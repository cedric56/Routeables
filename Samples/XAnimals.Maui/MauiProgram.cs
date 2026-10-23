using Reshapes.Maui.CommunityToolkit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Routeables.Abstraction;
using Routeables.Generated;
using Routeables.Locking;
using Routeables.Maui;
using Routeables.Segments;
using XAnimals.ViewModels;
using CommunityToolkit.Maui;

namespace XAnimals.Maui
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .UseRouteablesCommunityToolkit()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("Inter-Regular.otf", "InterRegular");
                    fonts.AddFont("Inter-SemiBold.otf", "InterSemibold");
                    fonts.AddFont("LoraDisplay-Regular.ttf", "LoraDisplay");
                });

            // Routes generated for the views of this project and the view models it references.
            builder.Services.AddGeneratedRoutes();
            builder.Services.AddDebugErrorSink();
            builder.Services.AddRouteCoordinator();
            builder.Services.AddLocking();
            builder.Services.AddHistoryRegion();
            builder.Services.AddShellRegions();
            // MAUI draws a FontImageSource on Windows only from a font file the application registered: from a system font (the emoji
            // here) it fails with "not supported for a relative URI". There the entries have no icon image (their glyph is still text).
            AnimalIcons.Register(glyph => OperatingSystem.IsWindows() ? null : new FontImageSource { Glyph = glyph, Size = 24 });
            builder.Services.RegisterMainRouteables<MainViewModel>(ServiceLifetime.Singleton);

#if DEBUG
            // A registration that cannot be built fails at start, with its name, instead of at the first navigation.
            builder.ConfigureContainer(new DefaultServiceProviderFactory(new ServiceProviderOptions { ValidateOnBuild = true }));
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
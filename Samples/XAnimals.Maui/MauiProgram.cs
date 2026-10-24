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
using XAnimals.Common;

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
                .UseRouteables<MainViewModel>()
                .UseCommunityToolkit()
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
            builder.Services.AddLayoutRegions();
            // MAUI draws a FontImageSource on Windows only from a font file the application registered: from a system font (the emoji
            // here) it fails with "not supported for a relative URI". There the icons are the pictures of Resources/Images (icon_<key>.png).
            AnimalIcons.Register((key, glyph) => OperatingSystem.IsWindows()
                ? ImageSource.FromFile($"icon_{key}.png")
                : new FontImageSource { Glyph = glyph, Size = 24 });
            builder.Services.AddAnimalModules();      // the modules of the sample (Birds): their pages are added to the menu

#if DEBUG
            // A registration that cannot be built fails at start, with its name, instead of at the first navigation.
            builder.ConfigureContainer(new DefaultServiceProviderFactory(new ServiceProviderOptions { ValidateOnBuild = true }));
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
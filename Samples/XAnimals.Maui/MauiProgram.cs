using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Reshapes.Maui.CommunityToolkit;
using Routeables.Generated;
using Routeables.Maui;
using XAnimals.Common;
using XAnimals.ViewModels;

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
                    fonts.AddFont("Lora-Regular.ttf", "Lora");
                });

            // Routes generated for the views of this project and the view models it references.
            builder.Services.AddGeneratedRoutes();
            // MAUI draws a FontImageSource on Windows only from a font file the application registered: from a system font (the emoji
            // here) it fails with "not supported for a relative URI". There the icons are the pictures of Resources/Images (icon_<key>.png).
            AnimalIcons.Register((key, glyph) => OperatingSystem.IsWindows()
                ? ImageSource.FromFile($"icon_{key}.png")
                : new FontImageSource { Glyph = glyph, Size = 24 });
            builder.Services.RegisterViewModels();    // the coordinator, the regions, the segments and the modules of the sample, as on every head

#if DEBUG
            // A registration that cannot be built fails at start, with its name, instead of at the first navigation.
            builder.ConfigureContainer(new DefaultServiceProviderFactory(new ServiceProviderOptions { ValidateOnBuild = true }));
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
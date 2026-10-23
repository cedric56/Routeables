using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Routeables.Abstraction;
using Routeables.Core;
using Routeables.Razor;
using Routeables.Razor.Services;
using Routeables.Services;

namespace Reshapes.Razor.MudBlazor;

public static class Extensions
{
    /// <summary>
    /// Registers the Routeables framework, services, routing engine, dialog services, and MudBlazor configurations 
    /// into the specified service collection.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="runtime">The runtime environment settings.</param>
    /// <param name="onCircuitProviderChanged">Optional callback executed when the circuit provider initializes.</param>
    /// <param name="position">The screen position where toast notifications should appear, defaulting to bottom-left.</param>
    /// <param name="maxDispayments">The maximum number of simultaneous notifications displayed, defaulting to 5.</param>
    public static void UseRouteablesMudBlazor(this IServiceCollection services,
        Runtime runtime,
        Func<IServiceProvider, Task>? onCircuitProviderChanged = null,
        RouteableNotificationPosition position = RouteableNotificationPosition.BottomLeft,
        int maxDispayments = 5
        )
    {
        services.AddMudServices(config =>
        {
            config.SnackbarConfiguration = new SnackbarConfiguration()
            {
                PreventDuplicates = false,
                PositionClass = position switch
                {
                    RouteableNotificationPosition.TopLeft => Defaults.Classes.Position.TopLeft,
                    RouteableNotificationPosition.TopCenter => Defaults.Classes.Position.TopCenter,
                    RouteableNotificationPosition.TopRight => Defaults.Classes.Position.TopRight,
                    RouteableNotificationPosition.BottomLeft => Defaults.Classes.Position.BottomLeft,
                    RouteableNotificationPosition.BottomCenter => Defaults.Classes.Position.BottomCenter,
                    RouteableNotificationPosition.BottomRight => Defaults.Classes.Position.BottomRight,
                    _ => Defaults.Classes.Position.TopRight
                },
                MaxDisplayedSnackbars = maxDispayments
            };
        });


        services.UseRouteables(runtime, onCircuitProviderChanged);
        services.UseRouteablesServices(
            sp => new ReshapeDialogService(sp.GetRequiredService<IDialogService>()),
            sp => new ReshapeContentDialogService(sp.GetRequiredService<IDialogService>()),
            sp => new ReshapeTaskDialogService(sp.GetRequiredService<IDialogService>()),
            sp => new ReshapeNotificationService(sp.GetRequiredService<ISnackbar>()),
            position,
            maxDispayments);
    }
}

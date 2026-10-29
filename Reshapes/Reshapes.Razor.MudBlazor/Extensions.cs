using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Routeables.Abstraction;
using Routeables.Core;
using Routeables.Razor;
using Routeables.Razor.Services;
using Routeables.Services;

namespace Reshapes.Razor.MudBlazor;

/// <summary>
/// 
/// </summary>
public static class Extensions
{
    /// <summary>
    /// Draws the dialogs, content dialogs, task dialogs and notifications with MudBlazor (dialogs and snackbars) and adds MudBlazor itself.
    /// Call it after <c>UseRouteables</c>; there is no need to call <c>UseRouteablesServices</c> as well.
    /// <code>
    /// services.UseRouteablesServer&lt;MainViewModel&gt;(r => r.AddGeneratedRoutes());
    /// services.UseMudBlazor();
    /// </code>
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="position">The screen position where toast notifications should appear, defaulting to bottom-left.</param>
    /// <param name="maxDispayments">The maximum number of simultaneous notifications displayed, defaulting to 5.</param>
    public static IServiceCollection UseMudBlazor(this IServiceCollection services,
        RouteableNotificationPosition position = RouteableNotificationPosition.BottomLeft,
        int maxDispayments = 5)
    {
        ArgumentNullException.ThrowIfNull(services);

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


        return services.UseRouteablesServices(
            sp => null!,
            //sp => new ReshapeDialogService(sp.GetRequiredService<IDialogService>()),
            //sp => new ReshapeContentDialogService(sp.GetRequiredService<IDialogService>()),
            //sp => new ReshapeTaskDialogService(sp.GetRequiredService<IDialogService>()),
            sp => new ReshapeNotificationService(sp.GetRequiredService<ISnackbar>()),
            position,
            maxDispayments);
    }
}

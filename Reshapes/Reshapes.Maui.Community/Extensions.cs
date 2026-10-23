using Routeables.Maui;
using Routeables.Maui.Services;

namespace Reshapes.Maui.CommunityToolkit;

public static class Extensions
{
    /// <summary>
    /// Registers Routeables for MAUI (<c>UseRouteables()</c> then <c>UseRouteablesServices()</c>) with the content dialog, task dialog, modal and notification services built on
    /// CommunityToolkit.Maui (popups and snackbars), and configures CommunityToolkit.Maui itself.
    /// Use it instead of <c>UseRouteables()</c>; do not call <c>UseMauiCommunityToolkit()</c> as well.
    /// </summary>
    /// <param name="builder">The MAUI app builder to configure.</param>
    /// <returns>The same <paramref name="builder"/>, for chaining.</returns>
    public static MauiAppBuilder UseRouteablesCommunityToolkit(this MauiAppBuilder builder)
    {
        return builder.UseRouteables().UseRouteablesServices(
            modalService: sp => new ReshapeModalService(sp.GetRequiredService<IRouteableDialogWindowContext>()),
            contentDialogService: sp => new ReshapeContentDialogService(sp.GetRequiredService<IRouteableDialogWindowContext>()),
            taskDialogService: sp => new ReshapeTaskDialogService(sp.GetRequiredService<IRouteableDialogWindowContext>()),
            notificationService: sp => new ReshapeNotificationService(sp.GetRequiredService<IRouteableDialogWindowContext>()));
    }
}

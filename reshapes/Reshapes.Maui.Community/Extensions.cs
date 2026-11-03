using Reshapes.Maui.Community;
using Routeables.Maui.Services;

namespace Reshapes.Maui.CommunityToolkit;

/// <summary>
/// 
/// </summary>
public static class Extensions
{
    /// <summary>
    /// Draws the modals, content dialogs, task dialogs and notifications with CommunityToolkit.Maui (popups and snackbars).
    /// Call it after <c>UseRouteables</c>; there is no need to call <c>UseRouteablesServices</c> as well. CommunityToolkit.Maui itself
    /// is set up the usual way, with <c>UseMauiCommunityToolkit()</c>.
    /// <code>
    /// builder.UseMauiApp&lt;App&gt;().UseMauiCommunityToolkit().UseRouteables&lt;MainViewModel&gt;().UseCommunityToolkit();
    /// </code>
    /// </summary>
    /// <param name="builder">The MAUI app builder to configure.</param>
    /// <returns>The same <paramref name="builder"/>, for chaining.</returns>
    public static MauiAppBuilder UseCommunityToolkit(this MauiAppBuilder builder)
    {
        return builder.UseRouteablesServices(
            messageBoxService: sp => new ReshapeMessageBoxService(sp.GetRequiredService<IRouteableDialogWindowContext>()),
            notificationService: sp => new ReshapeNotificationService(sp.GetRequiredService<IRouteableDialogWindowContext>(), sp.GetService<RouteableSystemNotifications>()));
    }
}

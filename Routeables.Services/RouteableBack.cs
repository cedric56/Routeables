using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;

namespace Routeables.Services;

/// <summary>
/// The answer of a host to the Back request of the system (the hardware button or gesture of Android, Alt+Left and the mouse back button
/// on a desktop, the back command of a window): go back one page of the router when there is one to go back to, and tell the system whether it
/// must leave it alone. A tab that has its own history (an entry with a <see cref="IRouteableEntry.Region"/>) goes back inside the tab first. Every host calls this one method; a host of your own calls it the same way.
/// </summary>
public static class RouteableBack
{
    /// <summary>
    /// The router a Back request goes through: the router of the region of the selected menu entry (a tab with its own history) when it has a
    /// page to go back to, otherwise the default router when it has one; <see langword="null"/> when neither has.
    /// </summary>
    /// <param name="provider">The container of the host.</param>
    public static IRouter? Target(IServiceProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        if (provider.GetService<IMainRouteables>()?.Routeable?.Region is { Length: > 0 } region)
        {
            IRouter? tab = null;
            try
            {
                tab = provider.GetRouter(region);
            }
            catch (InvalidOperationException)
            {
                // A region that is not registered has no history of its own: the default router answers.
            }

            if (tab is { CanGoBack: true })
                return tab;
        }

        return provider.GetService<IRouter>() is { CanGoBack: true } router ? router : null;
    }

    /// <summary>Whether a Back request has a page to go back to (see <see cref="Target"/>).</summary>
    /// <param name="provider">The container of the host.</param>
    public static bool CanGoBack(IServiceProvider provider) => Target(provider) is not null;

    /// <summary>
    /// Goes back one page, if there is one to go back to: in the tab that is selected when it has its own history (see <see cref="Target"/>),
    /// otherwise in the default router. The route change runs on its own: the call returns at once.
    /// </summary>
    /// <param name="provider">The container of the host.</param>
    /// <returns>
    /// <see langword="true"/> when a route change was started: the request is handled, the system must not close the window or leave the
    /// application. <see langword="false"/> when the router is on its first page: the system does what it does.
    /// </returns>
    public static bool TryGoBack(IServiceProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        if (Target(provider) is not { } router)
            return false;

        _ = GoBackAsync(provider, router);
        return true;
    }

    // An error of the route change (a page that refuses to be left is a cancel, not an error) goes to the sink of the application.
    private static async Task GoBackAsync(IServiceProvider provider, IRouter router)
    {
        try
        {
            var result = await router.GoBackAsync();

            // The menu shows what the default router shows: it is told, as it is when its own view model goes back. A tab (a region)
            // is shown by its own region.
            if (result.Success)
                RouteableMenu.Moved(provider, router);
        }
        catch (Exception ex)
        {
            RouteableErrors.Report(provider, ex, typeof(RouteableBack));
        }
    }
}

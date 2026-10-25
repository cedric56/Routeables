using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;

namespace Routeables.Services;

/// <summary>
/// Tells the main view model that the router moved by something other than a selection of its menu (the Back button, a link, the
/// pages restored after the system closed the application): the menu then shows the page that is current and selects its entry.
/// </summary>
internal static class RouteableMenu
{
    /// <summary>
    /// Tells the menu when <paramref name="router"/> is the default router, which is the one the menu shows (a region is shown by its own
    /// zone). The call is moved to the thread of the interface when the caller is on another.
    /// </summary>
    public static void Moved(IServiceProvider provider, IRouter router)
    {
        if (!ReferenceEquals(router, provider.GetService<IRouter>()) || provider.GetService<IMainViewModel>() is not { } main)
            return;

        var viewModel = router.CurrentViewModel;
        if (provider.GetService<IRouteableDispatcher>() is { } dispatcher && !dispatcher.CheckAccess())
            dispatcher.Post(() => main.OnRouterMoved(viewModel));
        else
            main.OnRouterMoved(viewModel);
    }
}

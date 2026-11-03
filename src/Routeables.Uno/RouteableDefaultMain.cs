using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Routeables.Core;

namespace Routeables.Uno;

/// <summary><c>UseRouteables</c> for an application that has no menu: <see cref="RouteableDefaultMainViewModel"/> is its main view model.</summary>
public static class RouteableDefaultMain
{
    /// <summary>
    /// Adds Routeables to the application without a main view model of your own. Say which page opens first with <c>AddHome("/route")</c> in <paramref name="configure"/>.
    /// </summary>
    /// <param name="application">The application.</param>
    /// <param name="configure">Optional: your own registrations (routes, modules, services).</param>
    /// <param name="afterBuild">Optional callback executed immediately after the service provider is built.</param>
    /// <returns>The container.</returns>
    public static IServiceProvider UseRouteables(
        this Application application,
        Action<IServiceCollection>? configure = null,
        Action<IServiceProvider>? afterBuild = null)
        => application.UseRouteables<RouteableDefaultMainViewModel>(configure, afterBuild);

    /// <summary>
    /// Adds Routeables to the application without a main view model of your own, and opens its main window.
    /// </summary>
    /// <param name="application">The application.</param>
    /// <param name="configure">Your own registrations (routes, modules, services).</param>
    /// <param name="view">Creates the root of the window.</param>
    /// <param name="configureWindow">Optional, to set the title, the size or anything else before the window is activated.</param>
    /// <param name="afterBuild">Optional callback executed immediately after the service provider is built.</param>
    /// <param name="onDispose">Optional callback executed when the window closes.</param>
    /// <returns>The container.</returns>
    public static IServiceProvider UseRouteables(
        this Application application,
        Action<IServiceCollection> configure,
        Func<FrameworkElement> view,
        Action<Window>? configureWindow = null,
        Action<IServiceProvider>? afterBuild = null,
        Action? onDispose = null)
        => application.UseRouteables<RouteableDefaultMainViewModel>(configure, view, configureWindow, afterBuild, onDispose);
}

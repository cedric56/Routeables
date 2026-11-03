using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Routeables.Core;

namespace Routeables.Wpf;

/// <summary><c>UseRouteables</c> for an application that has no menu: <see cref="RouteableDefaultMainViewModel"/> is its main view model.</summary>
public static class RouteableDefaultMain
{
    /// <summary>
    /// Adds Routeables to the application without a main view model of your own. Say which page opens first with <c>AddHome("/route")</c> in <paramref name="configure"/>.
    /// </summary>
    /// <param name="application">The application.</param>
    /// <param name="configure">Registers the routes and the services of the application.</param>
    /// <param name="afterBuild">Called with the container once it is built.</param>
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
    /// <param name="configure">Registers the routes and the services of the application.</param>
    /// <param name="view">Creates the main view.</param>
    /// <param name="configureWindow">Sets the window up (title, size, icon).</param>
    /// <param name="afterBuild">Called with the container once it is built.</param>
    /// <param name="onDispose">Called when the window is closed, before the container is disposed.</param>
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

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Microsoft.Extensions.DependencyInjection;
using Routeables.Core;

namespace Routeables.Avalonia;

/// <summary><c>UseRouteables</c> for an application that has no menu: <see cref="RouteableDefaultMainViewModel"/> is its main view model.</summary>
public static class RouteableDefaultMain
{
    /// <summary>
    /// Adds Routeables to an <see cref="AppBuilder"/> without a main view model of your own. Say which page opens first with
    /// <c>AddHome("/route")</c> in <paramref name="configure"/>.
    /// <code>
    /// AppBuilder.Configure&lt;App&gt;().UseRouteables(r => { r.AddGeneratedRoutes(); r.AddHome("/hello/World"); });
    /// </code>
    /// </summary>
    /// <param name="builder">The builder to configure.</param>
    /// <param name="configure">Optional: your own registrations (routes, modules, services).</param>
    /// <param name="afterBuild">Optional callback executed immediately after the service provider is built.</param>
    /// <param name="onDispose">Optional callback executed when the application window closes and disposes.</param>
    /// <param name="view">Optional: creates the main view from the lifetime of the application.</param>
    /// <returns>The same <see cref="AppBuilder"/>.</returns>
    public static AppBuilder UseRouteables(
        this AppBuilder builder,
        Action<IServiceCollection>? configure = null,
        Action<IServiceProvider>? afterBuild = null,
        Action? onDispose = null,
        Func<IApplicationLifetime?, Control>? view = null)
        => builder.UseRouteables<RouteableDefaultMainViewModel>(configure, afterBuild, onDispose, view);
}

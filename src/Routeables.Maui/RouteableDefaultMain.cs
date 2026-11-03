using Microsoft.Extensions.DependencyInjection;
using Routeables.Core;

namespace Routeables.Maui;

/// <summary><c>UseRouteables</c> for an application that has no menu: <see cref="RouteableDefaultMainViewModel"/> is its main view model.</summary>
public static class RouteableDefaultMain
{
    /// <summary>
    /// Starts Routeables on MAUI without a main view model of your own. Say which page opens first with <c>AddHome("/route")</c> in <paramref name="configure"/>.
    /// <code>
    /// builder.UseMauiApp&lt;App&gt;().UseRouteables(r => { r.AddGeneratedRoutes(); r.AddHome("/hello/World"); });
    /// </code>
    /// </summary>
    /// <param name="builder">The MAUI app builder to configure.</param>
    /// <param name="configure">Optional: your own registrations (routes, modules, services).</param>
    /// <returns>The same <paramref name="builder"/>, for chaining.</returns>
    public static MauiAppBuilder UseRouteables(this MauiAppBuilder builder, Action<IServiceCollection>? configure = null)
        => builder.UseRouteables<RouteableDefaultMainViewModel>(configure);
}

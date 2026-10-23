using Routeables.Abstraction;
using Routeables.Core;
using Routeables.Routing;
using Routeables.Services;

namespace Routeables.Maui;

/// <summary>
/// Extension methods for wiring up the Routeables IoC container and its associated services
/// into a MAUI application's <see cref="MauiAppBuilder"/>.
/// </summary>
public static class Extensions
{
    /// <summary>
    /// Registers the router, the arguments factory, the property injection, the environment information and the theme service.
    /// </summary>
    /// <remarks>
    /// The modal, content dialog, task dialog, dialog and notification services are optional: add
    /// <c>UseRouteablesServices()</c> from <c>Routeables.Maui.Services</c> to get them.
    /// </remarks>
    /// <param name="builder">The MAUI app builder to configure.</param>
    /// <returns>The same <paramref name="builder"/>, for chaining.</returns>
    public static MauiAppBuilder UseRouteables(this MauiAppBuilder builder)
    {
        builder.Services.AddRouteableHost(Runtime.Maui,
            new RouteablePropertyEntry(typeof(QueryPropertyAttribute), a => ((QueryPropertyAttribute)a).Name, FromContainer: false));
        builder.Services.AddSingleton<IRouteableRequestedThemeService, RouteableRequestThemeService>();
        return builder;
    }
}

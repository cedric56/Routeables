using System.Diagnostics.CodeAnalysis;
using Routeables.Abstraction;
using Routeables.Core;
using Routeables.Maui.Services;
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
    /// Starts Routeables on MAUI: registers the router, the arguments factory, the property injection, the environment information and the
    /// theme service, and <typeparamref name="TMain"/> as the main view model (the menu), one for the application.
    /// <code>
    /// builder.UseMauiApp&lt;App&gt;().UseRouteables&lt;MainViewModel&gt;(r => r.AddGeneratedRoutes());
    /// </code>
    /// </summary>
    /// <remarks>
    /// It registers the modal, content dialog, task dialog, dialog and notification services too (<c>UseRouteablesServices()</c> of
    /// <c>Routeables.Maui.Services</c>); call that one again only to replace a service.
    /// </remarks>
    /// <typeparam name="TMain">The main view model.</typeparam>
    /// <param name="builder">The MAUI app builder to configure.</param>
    /// <param name="configure">Optional: your own registrations (routes, modules, services), the same block as on the other hosts.</param>
    /// <returns>The same <paramref name="builder"/>, for chaining.</returns>
    public static MauiAppBuilder UseRouteables<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TMain>(
        this MauiAppBuilder builder, Action<IServiceCollection>? configure = null)
        where TMain : class, IMainRouteables
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddRouteableHost(Runtime.Maui,
            new RouteablePropertyEntry(typeof(QueryPropertyAttribute), a => ((QueryPropertyAttribute)a).Name, FromContainer: false));
        builder.Services.AddSingleton<IRouteableRequestedThemeService, RouteableRequestThemeService>();
        builder.Services.RegisterMainRouteables<TMain>();
        builder.UseRouteablesServices();
        configure?.Invoke(builder.Services);
        return builder;
    }
}

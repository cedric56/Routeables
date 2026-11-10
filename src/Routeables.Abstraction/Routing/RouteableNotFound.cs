using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Routeables.Abstraction.Routing;

/// <summary>The route shown when <c>GoToAsync</c> finds no route for an address (a 404 page). See <see cref="RouteableNotFoundExtensions.UseNotFoundRoute"/>.</summary>
/// <param name="Route">The route of the page, registered like any other.</param>
public sealed record RouteableNotFound(string Route)
{
    /// <summary>The name of the value that carries the address that was asked for (<c>args.Values</c> of the page).</summary>
    public const string UrlValue = "notFoundUrl";
}

/// <summary>Registers the page shown when no route matches.</summary>
public static class RouteableNotFoundExtensions
{
    /// <summary>
    /// When <c>GoToAsync("/nope")</c> matches no route, goes to <paramref name="route"/> instead, with the address asked for in the value
    /// <see cref="RouteableNotFound.UrlValue"/> (<c>notFoundUrl</c>). The page is a route like any other: register it with <c>[Routeable]</c> or <c>RegisterRoute</c>.
    /// The result is that of the navigation to the page, so a caller that reports failures does not report the error twice. Without this call a
    /// route that matches nothing is a failed result, as before. A missing page that is itself missing is a failed result too (no loop).
    /// </summary>
    public static IServiceCollection UseNotFoundRoute(this IServiceCollection services, string route = "/not-found")
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(route);
        services.RemoveAll<RouteableNotFound>();
        services.AddSingleton(new RouteableNotFound(route));
        return services;
    }
}

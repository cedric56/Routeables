using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;

namespace Routeables.Blazor;

/// <summary>Registers the authorization of Blazor for the routes of Routeables.</summary>
public static class AuthorizationExtensions
{
    /// <summary>
    /// Makes <c>[Authorize]</c> and <c>[AllowAnonymous]</c> count on the view models (and views) of Routeables: registers
    /// <see cref="RouteableAuthorizationGuard"/> and the authorization services (<c>AddAuthorizationCore</c>). The application still
    /// registers its authentication (an <c>AuthenticationStateProvider</c>) and logging, as any Blazor application with sign-in does.
    /// <code>
    /// services.AddRouteableAuthorization(o => o.SignInRoute = "/sign-in");
    /// </code>
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional: where the visitors who are turned away go.</param>
    public static IServiceCollection AddRouteableAuthorization(this IServiceCollection services, Action<RouteableAuthorizationOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        var options = new RouteableAuthorizationOptions();
        configure?.Invoke(options);
        services.AddAuthorizationCore();
        services.AddSingleton(options);
        services.AddScoped<IRouteGuard, RouteableAuthorizationGuard>();
        return services;
    }
}

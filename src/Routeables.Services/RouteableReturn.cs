using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Routeables.Abstraction;

namespace Routeables.Services;

/// <summary>
/// Remembers the route a guard turned away, so the page the guard sent the user to (a sign-in) can take them back to it.
/// </summary>
/// <example>
/// <code>
/// // in the guard
/// return returns.RedirectFrom(change, "/sign-in");
///
/// // in the sign-in view model, once the user is signed in
/// await router.GoToAsync(returns.Take() ?? "/home");
/// </code>
/// </example>
public sealed class RouteableReturn
{
    private readonly object gate = new();
    private string? pending;

    /// <summary>The route that was turned away and not taken back yet.</summary>
    public string? Pending
    {
        get { lock (gate) return pending; }
    }

    /// <summary>Remembers a route; it replaces the one remembered before.</summary>
    public void Remember(string? route)
    {
        lock (gate)
            pending = string.IsNullOrWhiteSpace(route) ? null : route;
    }

    /// <summary>The remembered route, which is forgotten; <see langword="null"/> when there is none.</summary>
    public string? Take()
    {
        lock (gate)
        {
            var route = pending;
            pending = null;
            return route;
        }
    }

    /// <summary>
    /// Remembers the route the change was going to and redirects to <paramref name="route"/>. A change with no route of its own
    /// (a view and a view model given to <see cref="IRouter.GoAsync"/>) remembers nothing. Only a <c>Push</c> is remembered, since
    /// Back, Forward and Pop are cancelled rather than redirected.
    /// </summary>
    public RouteGuardDecision RedirectFrom(RouteableRoutingEventArgs change, string route)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (change.Kind == RouteableKind.Push)
            Remember(change.To.Args.Route);
        return RouteGuardDecision.Redirect(route);
    }
}

/// <summary>Registration of <see cref="RouteableReturn"/>.</summary>
public static class RouteableReturnExtensions
{
    /// <summary>Registers <see cref="RouteableReturn"/> as a singleton.</summary>
    public static IServiceCollection AddRouteableReturn(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<RouteableReturn>();
        return services;
    }
}

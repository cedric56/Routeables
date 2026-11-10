using Routeables.Abstraction.Routing;

namespace Routeables.Abstraction.Guards;

/// <summary>What a <see cref="IRouteGuard"/> decides: let the route change go on, cancel it, or send the user to another route.</summary>
public sealed class RouteGuardDecision
{
    private RouteGuardDecision(string? redirectTo, bool allowed)
    {
        RedirectTo = redirectTo;
        IsAllowed = allowed;
    }

    /// <summary>The route change goes on (the next guard is asked).</summary>
    public static RouteGuardDecision Allow { get; } = new(null, true);

    /// <summary>The route change stops: the result is cancelled, the current page stays.</summary>
    public static RouteGuardDecision Cancel { get; } = new(null, false);

    /// <summary>
    /// The route change stops and the router goes to <paramref name="route"/> instead (as <c>router.GoToAsync(route)</c>, once the
    /// first route change has ended). A redirect applies to a new page (<see cref="RouteableKind.Push"/>); Back, Forward, Pop, Root and MoveTo
    /// are cancelled. Redirects that lead to a redirect are followed up to eight times, then the route change fails.
    /// </summary>
    public static RouteGuardDecision Redirect(string route)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(route);
        return new RouteGuardDecision(route, false);
    }

    /// <summary>Whether the route change goes on.</summary>
    public bool IsAllowed { get; }

    /// <summary>The route to go to instead, or <see langword="null"/>.</summary>
    public string? RedirectTo { get; }
}

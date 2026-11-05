namespace Routeables.Blazor;

/// <summary>
/// Where <see cref="RouteableAuthorizationGuard"/> sends a visitor it turns away. Each route is optional: without it, the route
/// change is cancelled and the current page stays.
/// </summary>
public sealed class RouteableAuthorizationOptions
{
    /// <summary>The route of the sign-in page, for a visitor who is not signed in. Mark its view model <c>[AllowAnonymous]</c>.</summary>
    public string? SignInRoute { get; set; }

    /// <summary>The route of the page that tells a signed-in visitor that this page is not for them (the roles or the policy refuse). Mark its view model <c>[AllowAnonymous]</c>.</summary>
    public string? ForbiddenRoute { get; set; }
}

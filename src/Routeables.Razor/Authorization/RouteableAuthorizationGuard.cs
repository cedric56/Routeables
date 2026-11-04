using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using System.Security.Claims;

namespace Routeables.Razor;

/// <summary>
/// The authorization of Blazor, for the routes of Routeables: a page whose view model (or view) carries <c>[Authorize]</c>
/// (<see cref="AuthorizeAttribute"/>, with its <c>Roles</c> and <c>Policy</c>) is entered only by a visitor the
/// <see cref="AuthenticationStateProvider"/> and the authorization policies accept. <c>[AllowAnonymous]</c> opens a page again.
/// Registered by <see cref="AuthorizationExtensions.AddRouteableAuthorization"/>.
/// </summary>
/// <remarks>
/// A visitor who is not signed in goes to <see cref="RouteableAuthorizationOptions.SignInRoute"/>, a signed-in visitor who is refused to
/// <see cref="RouteableAuthorizationOptions.ForbiddenRoute"/>; without a route the route change is cancelled. Only a new page is redirected
/// (<see cref="RouteGuardDecision.Redirect"/>); Back, Forward and the like are cancelled.
/// </remarks>
public sealed class RouteableAuthorizationGuard(IServiceProvider provider, RouteableAuthorizationOptions options) : IRouteGuard
{
    /// <inheritdoc />
    public int Order => -100;     // before the guards of the application: they need not repeat the rule

    /// <inheritdoc />
    public async Task<RouteGuardDecision> CanEnterAsync(RouteableRoutingEventArgs change, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(change);
        Type[] types = [change.To.ViewModel.GetType(), change.To.View.GetType()];
        if (types.Any(t => t.GetCustomAttributes(typeof(IAllowAnonymous), inherit: true).Length > 0))
            return RouteGuardDecision.Allow;

        var data = types.SelectMany(t => t.GetCustomAttributes(typeof(IAuthorizeData), inherit: true).OfType<IAuthorizeData>()).ToList();
        if (data.Count == 0)
            return RouteGuardDecision.Allow;

        var authentication = provider.GetService<AuthenticationStateProvider>()
            ?? throw new InvalidOperationException(
                "A page carries [Authorize] but no AuthenticationStateProvider is registered. Register the authentication of the application "
                + "(AddCascadingAuthenticationState and an AuthenticationStateProvider) before the first route change.");
        ClaimsPrincipal user = (await authentication.GetAuthenticationStateAsync()).User;

        var policy = await AuthorizationPolicy.CombineAsync(provider.GetRequiredService<IAuthorizationPolicyProvider>(), data);
        if (policy is null)
            return RouteGuardDecision.Allow;

        var result = await provider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, null, policy);
        if (result.Succeeded)
            return RouteGuardDecision.Allow;

        var route = user.Identity?.IsAuthenticated == true ? options.ForbiddenRoute : options.SignInRoute;
        return route is null || IsAddress(change, route) ? RouteGuardDecision.Cancel : RouteGuardDecision.Redirect(route);
    }

    // The sign-in page that forgot [AllowAnonymous] would send the visitor to itself: cancel instead.
    private static bool IsAddress(RouteableRoutingEventArgs change, string route)
        => string.Equals(change.To.Args.Route?.Trim('/'), route.Trim('/'), StringComparison.OrdinalIgnoreCase);
}

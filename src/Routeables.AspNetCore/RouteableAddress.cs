using Microsoft.AspNetCore.Http;

namespace Routeables.AspNetCore;

/// <summary>
/// The address of a route on this site. One place turns a route into an address, so that the links of the menu and the redirect of a page agree
/// and neither can send the visitor to another site.
/// </summary>
public static class RouteableAddress
{
    /// <summary>
    /// The address of a route: a path that starts with one <c>/</c> and with the base path of the application (<paramref name="pathBase"/>) in front
    /// when it is hosted under a sub-path (<c>/app</c>). The slashes and back slashes at the start of the route are dropped: a browser reads
    /// <c>//evil.example</c> and <c>/\evil.example</c> as the address of another site, so a route can never begin with them.
    /// </summary>
    /// <param name="route">The route (<c>cats</c>, <c>/cats?x=1</c>).</param>
    /// <param name="pathBase">The <c>Request.PathBase</c> of the application, when it is not hosted at the root.</param>
    public static string Of(string? route, PathString pathBase = default)
        => pathBase.Add("/" + (route ?? string.Empty).TrimStart('/', '\\')).Value ?? "/";
}

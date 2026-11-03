using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;

namespace Routeables.Services;

/// <summary>
/// Turns an address that comes from outside the application (a link opened on a phone, a protocol registered on a desktop, a command-line
/// argument) into a route change. The hosts receive the address in their own way (an Android intent, an iOS universal link, the
/// arguments of a process): they all end by calling <see cref="GoToAsync"/>.
/// </summary>
/// <remarks>
/// The address is typed by someone else: only a route that is registered is reached (an unknown one is a failed result, not an exception),
/// the route guards (<see cref="IRouteGuard"/>) apply as for any route change, and <c>schemes</c> limits the addresses that are accepted.
/// </remarks>
public static class RouteableDeepLinks
{
    /// <summary>
    /// The route of an address. <c>https://example.com/orders/42?x=1</c> is <c>/orders/42?x=1</c>; for an application's own scheme the
    /// first segment is the name after the scheme: <c>myapp://orders/42?x=1</c> is <c>/orders/42?x=1</c>, and so is <c>myapp:///orders/42?x=1</c>.
    /// The fragment is left out; the characters stay escaped, as the router reads them.
    /// </summary>
    /// <param name="uri">The address.</param>
    /// <param name="schemes">The schemes that are accepted (<c>"myapp"</c>, <c>"https"</c>); <see langword="null"/> or empty accepts any.</param>
    /// <param name="route">The route, when the address has one.</param>
    /// <returns><see langword="false"/> for a relative address, a scheme that is not accepted, or an address with no path (<c>myapp://</c>).</returns>
    public static bool TryGetRoute(Uri? uri, IReadOnlyCollection<string>? schemes, out string route)
    {
        route = string.Empty;
        if (uri is null || !uri.IsAbsoluteUri)
            return false;

        if (schemes is { Count: > 0 } && !schemes.Contains(uri.Scheme, StringComparer.OrdinalIgnoreCase))
            return false;

        var isWeb = uri.Scheme is "http" or "https";
        var path = isWeb || string.IsNullOrEmpty(uri.Host)
            ? uri.AbsolutePath
            : "/" + uri.Host + (uri.AbsolutePath == "/" ? string.Empty : uri.AbsolutePath);

        // "myapp:///" and "myapp://" name nothing.
        path = "/" + path.Trim('/');
        if (path == "/")
            return false;

        route = path + uri.Query;
        return true;
    }

    /// <summary>The route of an address, without a limit on the schemes.</summary>
    public static bool TryGetRoute(Uri? uri, out string route) => TryGetRoute(uri, null, out route);

    /// <summary>
    /// Goes to the route of an address, on the default router. Call it on the thread of the interface, once the application is started
    /// and its first page is shown (a link that starts the application is kept until then, see the guide).
    /// </summary>
    /// <param name="provider">The container of the host.</param>
    /// <param name="uri">The address.</param>
    /// <param name="schemes">The schemes that are accepted; <see langword="null"/> or empty accepts any.</param>
    /// <param name="cancellationToken">Cancels the route change.</param>
    /// <returns>The result of the route change; failed (with an <see cref="ArgumentException"/>) when the address has no route or is not accepted.</returns>
    public static async Task<RouteableResult> GoToAsync(
        IServiceProvider provider, Uri uri, IReadOnlyCollection<string>? schemes = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(uri);

        if (!TryGetRoute(uri, schemes, out var route))
            return RouteableResult.Fail(new ArgumentException($"The address \"{uri}\" has no route that this application accepts.", nameof(uri)));

        var router = provider.GetRequiredService<IRouter>();
        var result = await router.GoToAsync(route, null, null, cancellationToken);

        // The menu shows what the router shows: it is told, as it is when its own selection changes the page.
        if (result.Success)
            RouteableMenu.Moved(provider, router);

        return result;
    }
}

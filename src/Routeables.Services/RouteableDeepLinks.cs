using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction.Guards;
using Routeables.Abstraction.Routing;

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
    /// <summary>The longest address (<see cref="Uri.OriginalString"/>) that is accepted, 4096 characters by default; a longer one is refused. Set it once at startup.</summary>
    public static int MaxLength { get; set; } = 4096;

    /// <summary>
    /// The route of an address. <c>https://example.com/orders/42?x=1</c> is <c>/orders/42?x=1</c> once <c>example.com</c> is given as a host (see the overload with the hosts); for an application's own scheme the
    /// first segment is the name after the scheme: <c>myapp://orders/42?x=1</c> is <c>/orders/42?x=1</c>, and so is <c>myapp:///orders/42?x=1</c>.
    /// The fragment is left out; the characters stay escaped, as the router reads them.
    /// </summary>
    /// <param name="uri">The address.</param>
    /// <param name="schemes">The schemes that are accepted (<c>"myapp"</c>, <c>"https"</c>); <see langword="null"/> or empty accepts any.</param>
    /// <param name="route">The route, when the address has one.</param>
    /// <returns><see langword="false"/> for a relative address, a scheme that is not accepted, a web address (no host is given here: use the overload with the hosts), or an address with no path (<c>myapp://</c>).</returns>
    public static bool TryGetRoute(Uri? uri, IReadOnlyCollection<string>? schemes, out string route)
        => TryGetRoute(uri, schemes, null, out route);

    /// <summary>
    /// The route of an address, for the schemes and the hosts that are accepted. The host of an <c>http</c> or <c>https</c> address is not part
    /// of the route (<c>https://evil.example/orders/42</c> is <c>/orders/42</c>): <paramref name="hosts"/> says whose addresses are the
    /// application's own, and any other host is refused.
    /// </summary>
    /// <param name="uri">The address.</param>
    /// <param name="schemes">The schemes that are accepted (<c>"myapp"</c>, <c>"https"</c>); <see langword="null"/> or empty accepts any.</param>
    /// <param name="hosts">The hosts of the <c>http</c> and <c>https</c> addresses that are accepted (<c>"example.com"</c>). <see langword="null"/> or empty
    /// accepts <b>no</b> web address: they are refused until the hosts are given. The address of an own scheme (<c>myapp://</c>) has no host in this
    /// sense and is not limited by it.</param>
    /// <param name="route">The route, when the address has one.</param>
    /// <returns><see langword="false"/> for a relative address, a scheme or a host that is not accepted, a web address when no host is given, or an address with no path.</returns>
    public static bool TryGetRoute(Uri? uri, IReadOnlyCollection<string>? schemes, IReadOnlyCollection<string>? hosts, out string route)
    {
        route = string.Empty;
        if (uri is null || !uri.IsAbsoluteUri)
            return false;

        // An address comes from outside the application (a message, a web page, another app): a huge one is refused before it is parsed.
        if (uri.OriginalString.Length > MaxLength)
            return false;

        if (schemes is { Count: > 0 } && !schemes.Contains(uri.Scheme, StringComparer.OrdinalIgnoreCase))
            return false;

        // The host of a web address is not part of the route: without the hosts that are the application's own, any web page or message
        // could send the address of another site and open a page of the application.
        var isWeb = uri.Scheme is "http" or "https";
        if (isWeb && (hosts is not { Count: > 0 } || !hosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase)))
            return false;

        var path = isWeb || string.IsNullOrEmpty(uri.Host)
            ? uri.AbsolutePath
            : AfterTheScheme(uri);

        // "myapp:///" and "myapp://" name nothing.
        path = "/" + path.Trim('/');
        if (path == "/")
            return false;

        route = path + uri.Query;
        return true;
    }

    // "myapp://Orders/42": what follows "//" as it was written. The Uri calls the first segment a host and changes it (lower case, the port
    // and the user name taken out), which would change a value such as the SKU12 of myapp://SKU12/details.
    private static string AfterTheScheme(Uri uri)
    {
        var text = uri.OriginalString;
        var at = text.IndexOf("://", StringComparison.Ordinal);
        if (at < 0)
            return "/" + uri.Host + (uri.AbsolutePath == "/" ? string.Empty : uri.AbsolutePath);

        var rest = text[(at + 3)..];
        var end = rest.IndexOfAny(['?', '#']);
        return "/" + (end >= 0 ? rest[..end] : rest);
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
    /// <param name="hosts">The hosts of the <c>http</c> and <c>https</c> addresses that are accepted; <see langword="null"/> or empty accepts no web address (see <see cref="TryGetRoute(Uri?, IReadOnlyCollection{string}?, IReadOnlyCollection{string}?, out string)"/>).</param>
    /// <returns>The result of the route change; failed (with an <see cref="ArgumentException"/>) when the address has no route or is not accepted.</returns>
    public static async Task<RouteableResult> GoToAsync(
        IServiceProvider provider, Uri uri, IReadOnlyCollection<string>? schemes = null, CancellationToken cancellationToken = default,
        IReadOnlyCollection<string>? hosts = null)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(uri);

        if (!TryGetRoute(uri, schemes, hosts, out var route))
            return RouteableResult.Fail(new ArgumentException($"The address \"{uri}\" has no route that this application accepts.", nameof(uri)));

        var router = provider.GetRequiredService<IRouter>();
        var result = await router.GoToAsync(route, null, null, cancellationToken);

        // The menu shows what the router shows: it is told, as it is when its own selection changes the page.
        if (result.Success)
            RouteableMenu.Moved(provider, router);

        return result;
    }
}

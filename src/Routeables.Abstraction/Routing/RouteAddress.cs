namespace Routeables.Abstraction.Routing;

/// <summary>
/// The one place that says whether two routes or addresses are the same. The router, the menus, the type guards, the controls of navigation and the
/// address bar of Blazor all ask here, so they agree. The rules:
/// <list type="bullet">
/// <item>A leading or closing slash does not count (<c>about</c>, <c>/about</c> and <c>/about/</c> are one route); a query is not part of a path.</item>
/// <item>The words of a path ignore the case (<c>/Orders</c> is <c>/orders</c>). The values of a placeholder do not, once the template is known:
/// <c>/items/aB</c> and <c>/items/Ab</c> are two pages when the identifier is a token or a slug.</item>
/// <item>A query is data: the order of its pairs and the case of its names do not count, the case of its values does, and a slash that ends a value is part of it.</item>
/// <item>An escaped address (<c>%20</c>, as a browser reports it) is the same as the one written plainly; an escaped slash stays a character, not a separator.</item>
/// </list>
/// </summary>
internal static class RouteAddress
{
    /// <summary>The address with exactly one leading slash. A protocol-relative <c>//host</c> (and <c>/\host</c>, read the same way by browsers) becomes <c>/host</c>: the address stays inside the application.</summary>
    /// <param name="route">The route or address.</param>
    internal static string Rooted(string route) => "/" + route.TrimStart('/', '\\');

    /// <summary>The route with exactly one slash before it and none at its end (<c>orders/42/</c> becomes <c>/orders/42</c>); the query, if any, is kept.</summary>
    /// <param name="route">The route.</param>
    internal static string Absolute(string? route)
    {
        // Only the slashes of the path: the last character of a value of the query ("?dir=/") belongs to the value.
        var text = route ?? string.Empty;
        var at = text.IndexOf('?');
        return "/" + (at < 0 ? text : text[..at]).Trim('/') + (at < 0 ? string.Empty : text[at..]);
    }

    /// <summary>The path of a route: without its query and without the slashes around it. Blank for none.</summary>
    /// <param name="route">The route, the template or the address.</param>
    internal static string PathOf(string? route) => WithoutFragment(route ?? string.Empty).Split('?')[0].Trim('/');

    // The fragment is for the browser, never for the route: "/about#top" is "/about", as the parser reads it.
    private static string WithoutFragment(string address) => address.IndexOf('#') is var hash and >= 0 ? address[..hash] : address;

    /// <summary>Whether two routes or templates have the same path, whatever their queries (the case and the slashes around the path do not count).</summary>
    /// <param name="a">The first route.</param>
    /// <param name="b">The second route.</param>
    internal static bool SamePath(string? a, string? b)
        => a is not null && b is not null && string.Equals(PathOf(a), PathOf(b), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether two addresses are the same page: the same path and the same query (see the rules of the type).
    /// </summary>
    /// <param name="a">The first address.</param>
    /// <param name="b">The second address.</param>
    /// <param name="templateOf">
    /// The template that serves an address (<see langword="null"/> when none does), to tell the words of a path from its values. Without it
    /// every difference of case is taken for a word: nothing is known to protect.
    /// </param>
    /// <returns><see langword="false"/> when either one is <see langword="null"/>.</returns>
    internal static bool Same(string? a, string? b, Func<string, string?>? templateOf = null)
    {
        if (a is null || b is null)
            return false;
        var (pathA, queryA) = SplitQuery(Rooted(WithoutFragment(a)));
        var (pathB, queryB) = SplitQuery(Rooted(WithoutFragment(b)));
        return SamePathOnly(Unescape(pathA.TrimEnd('/')), Unescape(pathB.TrimEnd('/')), templateOf)
               && SameQuery(queryA, queryB);
    }

    /// <summary>
    /// Whether two addresses are the same page for <paramref name="router"/>: when the router knows its templates (the default router does), the case of a
    /// <b>value</b> counts; for any other router (a double, a router of your own) every difference of case is taken for a word.
    /// </summary>
    /// <param name="router">The router that shows the pages.</param>
    /// <param name="a">The first address.</param>
    /// <param name="b">The second address.</param>
    internal static bool SameFor(IRouter router, string? a, string? b)
        => Same(a, b, router is IRouteTemplates templates ? templates.TemplateOf : null);

    private static bool SamePathOnly(string a, string b, Func<string, string?>? templateOf)
    {
        if (string.Equals(a, b, StringComparison.Ordinal))
            return true;
        if (!string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
            return false;
        if (templateOf?.Invoke(a) is not { } template)
            return true;

        var templateSegments = template.Split('?')[0].Split('/', StringSplitOptions.RemoveEmptyEntries);
        var segmentsA = a.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var segmentsB = b.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < segmentsA.Length && i < segmentsB.Length; i++)
        {
            if (string.Equals(segmentsA[i], segmentsB[i], StringComparison.Ordinal))
                continue;
            if (i >= templateSegments.Length || templateSegments[i].Contains('{'))
                return false;   // the case of a value changed: not the same page
        }

        return true;
    }

    private static (string Path, string? Query) SplitQuery(string address)
    {
        var at = address.IndexOf('?');
        return at < 0 ? (address, null) : (address[..at], address[(at + 1)..]);
    }

    private static bool SameQuery(string? a, string? b)
    {
        // "?x=1&y=2" is "?y=2&x=1": the pairs are put in order of name (the sort is stable, so the values of a repeated name keep their order).
        var pairsA = (a ?? string.Empty).Split('&', StringSplitOptions.RemoveEmptyEntries).OrderBy(p => SplitPair(p).Name, StringComparer.OrdinalIgnoreCase).ToArray();
        var pairsB = (b ?? string.Empty).Split('&', StringSplitOptions.RemoveEmptyEntries).OrderBy(p => SplitPair(p).Name, StringComparer.OrdinalIgnoreCase).ToArray();
        if (pairsA.Length != pairsB.Length)
            return false;
        for (var i = 0; i < pairsA.Length; i++)
        {
            var (nameA, valueA) = SplitPair(pairsA[i]);
            var (nameB, valueB) = SplitPair(pairsB[i]);
            if (!string.Equals(UnescapeQuery(nameA), UnescapeQuery(nameB), StringComparison.OrdinalIgnoreCase)
                || !string.Equals(UnescapeQuery(valueA), UnescapeQuery(valueB), StringComparison.Ordinal))
                return false;
        }
        return true;
    }

    private static (string Name, string Value) SplitPair(string pair)
    {
        var at = pair.IndexOf('=');
        return at < 0 ? (pair, string.Empty) : (pair[..at], pair[(at + 1)..]);
    }

    // In a query a plus is a space, as the parser reads it: "?q=a+b" and "?q=a%20b" are one address ("%2B" stays a plus).
    private static string UnescapeQuery(string text) => Unescape(text.Replace('+', ' '));

    private static string Unescape(string text)
    {
        // An escaped slash is not a separator: "/files/a%2Fb" is one segment and "/files/a/b" is two, so the first must stay escaped.
        try { return Uri.UnescapeDataString(text.Replace("%2F", "%252F", StringComparison.OrdinalIgnoreCase)); }
        catch (UriFormatException) { return text; }
    }
}

/// <summary>What the default router tells the comparison of addresses: the template that serves a route, to tell the words of a path from its values.</summary>
internal interface IRouteTemplates
{
    /// <summary>The template that serves <paramref name="route"/>; <see langword="null"/> when none does.</summary>
    /// <param name="route">The route or address.</param>
    string? TemplateOf(string route);
}

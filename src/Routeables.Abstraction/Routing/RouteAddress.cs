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
    /// <summary>The address with exactly one leading slash. A protocol-relative <c>//host</c> (and <c>/\host</c>, read the same way by browsers) becomes <c>/host</c>: the address stays inside the application. A browser drops the tabs and line breaks of an address, so <c>/&lt;tab&gt;/host</c> is <c>//host</c> to it: they are dropped here too.</summary>
    /// <param name="route">The route or address.</param>
    internal static string Rooted(string route) => "/" + route.TrimStart('/', '\\', '\t', '\r', '\n');

    /// <summary>The route with exactly one slash before it and none at its end (<c>orders/42/</c> becomes <c>/orders/42</c>); the query, if any, is kept.</summary>
    /// <param name="route">The route.</param>
    internal static string Absolute(string? route)
    {
        // Only the slashes of the path: the last character of a query value ("?dir=/") belongs to the value.
        var text = route ?? string.Empty;
        var at = QueryStart(text);
        return "/" + (at < 0 ? text : text[..at]).Trim('/') + (at < 0 ? string.Empty : text[at..]);
    }

    /// <summary>The path of a route: without its query and without the slashes around it. Blank for none.</summary>
    /// <param name="route">The route, the template or the address.</param>
    internal static string PathOf(string? route)
    {
        var text = WithoutFragment(route ?? string.Empty);
        var at = QueryStart(text);
        if (at >= 0) text = text[..at];
        return text.Trim('/');
    }

    // The query starts at the first question mark that is not an optional parameter's ("products/{tab?}"); -1 for none.
    internal static int QueryStart(string text)
    {
        var depth = 0;
        for (var i = 0; i < text.Length; i++)
        {
            // A brace that nothing closes is a character of a hand-typed address, not the start of a placeholder.
            if (text[i] == '{' && text.IndexOf('}', i) >= 0) depth++;
            else if (text[i] == '}' && depth > 0) depth--;
            // Inside braces a question mark is the "optional" mark only when the placeholder ends right after it ("{tab?}", "{tab:int?}"): in
            // "/notes/a{b?tab=x}" it starts the query, as the browser reads it.
            else if (text[i] == '?' && (depth == 0 || (i + 1 < text.Length && text[i + 1] != '}'))) return i;
        }
        return -1;
    }

    // The fragment is for the browser, never for the route: "/about#top" is "/about", as the parser reads it.
    private static string WithoutFragment(string address) => address.IndexOf('#') is var hash and >= 0 ? address[..hash] : address;

    /// <summary>Whether two routes or templates have the same path, whatever their queries (the case, the slashes around the path and its empty segments do not count, and an escaped path is the one written plainly).</summary>
    /// <param name="a">The first route.</param>
    /// <param name="b">The second route.</param>
    internal static bool SamePath(string? a, string? b)
        => a is not null && b is not null
           && string.Equals(Unescape(Collapsed("/" + PathOf(a))), Unescape(Collapsed("/" + PathOf(b))), StringComparison.OrdinalIgnoreCase);

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
        return SamePathOnly(Unescape(Collapsed(pathA)), Unescape(Collapsed(pathB)), templateOf)
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
        // The path is unescaped here: a question mark or number sign that was escaped is escaped again, or the parser would end the path at it.
        if (templateOf?.Invoke(a.Replace("?", "%3F").Replace("#", "%23")) is not { } template)
            return true;

        var templateSegments = (QueryStart(template) is var queryAt and >= 0 ? template[..queryAt] : template).Split('/', StringSplitOptions.RemoveEmptyEntries);
        var segmentsA = a.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var segmentsB = b.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < segmentsA.Length && i < segmentsB.Length; i++)
        {
            if (string.Equals(segmentsA[i], segmentsB[i], StringComparison.Ordinal))
                continue;
            if (i >= templateSegments.Length || templateSegments[i].Contains('{'))
                return false; // only the case of a value differs: not the same page
        }

        return true;
    }

    // The parser skips empty segments: "/a//b/" is the page "/a/b".
    private static string Collapsed(string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length == 0 ? string.Empty : "/" + string.Join('/', segments);
    }

    private static (string Path, string? Query) SplitQuery(string address)
    {
        var at = QueryStart(address);
        return at < 0 ? (address, null) : (address[..at], address[(at + 1)..]);
    }

    private static bool SameQuery(string? a, string? b)
    {
        // "?x=1&y=2" is "?y=2&x=1". A name given twice counts once, with its last value, as the parser reads it (RouteParser.ParseQuery): "?a=1&a=2" is "?a=2".
        var valuesA = Values(a);
        var valuesB = Values(b);
        if (valuesA.Count != valuesB.Count)
            return false;
        foreach (var (name, valueA) in valuesA)
            if (!valuesB.TryGetValue(name, out var valueB) || !string.Equals(valueA, valueB, StringComparison.Ordinal))
                return false;
        return true;
    }

    private static Dictionary<string, string> Values(string? query)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in (query ?? string.Empty).Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var (name, value) = SplitPair(pair);
            values[UnescapeQuery(name)] = UnescapeQuery(value);
        }
        return values;
    }

    private static (string Name, string Value) SplitPair(string pair)
    {
        var at = pair.IndexOf('=');
        return at < 0 ? (pair, string.Empty) : (pair[..at], pair[(at + 1)..]);
    }

    // In a query a plus is a space, as the parser reads it: "?q=a+b" and "?q=a%20b" are one address ("%2B" stays a plus).
    // A slash is not a separator in a query: "?dir=a/b" and "?dir=a%2Fb" are one address, as the parser reads them (RouteParser.DecodeQuery).
    private static string UnescapeQuery(string text)
    {
        try { return Uri.UnescapeDataString(text.Replace('+', ' ')); }
        catch (UriFormatException) { return text; }
    }

    private static string Unescape(string text)
    {
        // An escaped slash is not a separator: "/files/a%2Fb" is one segment and "/files/a/b" is two, so they must stay different.
        // Unescape each piece between escaped slashes, escape its percent signs again and join the pieces with "%2F". No two different texts give
        // the same result ("a%2Fb" stays "a%2Fb", "a%252Fb" (the text "a%2Fb") becomes "a%252Fb"), and no character stands for a slash.
        try
        {
            var pieces = text.Replace("%2f", "%2F").Split("%2F");
            return string.Join("%2F", pieces.Select(piece => Uri.UnescapeDataString(piece).Replace("%", "%25")));
        }
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

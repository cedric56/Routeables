using Routeables.Abstraction.Routing;
using System.Text.RegularExpressions;

namespace Routeables.Routing;

// The canonical text of an address: the values of the typed placeholders written the way Build writes them, so that every spelling is one page.
public static partial class RouteParser
{
    private static bool HasCanonicalText(string type)
        => type.Equals("int", StringComparison.OrdinalIgnoreCase) || type.Equals("long", StringComparison.OrdinalIgnoreCase)
           || type.Equals("bool", StringComparison.OrdinalIgnoreCase) || type.Equals("guid", StringComparison.OrdinalIgnoreCase)
           || type.Equals("decimal", StringComparison.OrdinalIgnoreCase) || type.Equals("double", StringComparison.OrdinalIgnoreCase);

    private static bool TryCanonical(string raw, string type, out string canonical)
    {
        canonical = raw;
        if (!TryConvert(raw, type, out var value) || value is null)
            return false;
        canonical = Format(value, type);
        return true;
    }

    private static bool IsTyped(Match m) => m.Success && HasCanonicalText(m.Groups["type"].Value);

    private static bool HasTyped(Match[] matches)
    {
        foreach (var m in matches)
            if (IsTyped(m))
                return true;
        return false;
    }

    // Whether Canonical would write a value of the address differently. It reads the address in place, and allocates only for the values it checks.
    private static bool WouldChange(ParsedTemplate parsed, string uri)
    {
        var hash = uri.IndexOf('#');
        var end = hash < 0 ? uri.Length : hash;
        var at = RouteAddress.QueryStart(uri, end);
        var pathEnd = at < 0 ? end : at;

        var position = 0;
        for (var start = 0; start <= pathEnd && position < parsed.SegmentMatches.Length;)
        {
            var slash = uri.IndexOf('/', start, pathEnd - start);
            var stop = slash < 0 ? pathEnd : slash;
            if (stop > start)
            {
                var m = parsed.SegmentMatches[position++];
                if (IsTyped(m) && !m.Groups["rest"].Success
                    && Uri.UnescapeDataString(uri.Substring(start, stop - start)) is var raw
                    && TryCanonical(raw, m.Groups["type"].Value, out var canonical) && canonical != raw)
                    return true;
            }
            if (slash < 0)
                break;
            start = slash + 1;
        }

        if (at < 0 || !HasTyped(parsed.QueryMatches))
            return false;
        for (var start = at + 1; start <= end;)
        {
            var amp = uri.IndexOf('&', start, end - start);
            var stop = amp < 0 ? end : amp;
            var eq = uri.IndexOf('=', start, stop - start);
            if (eq >= 0)
            {
                var name = RouteAddress.DecodeQuery(uri.Substring(start, eq - start));
                foreach (var q in parsed.QueryMatches)
                {
                    if (!IsTyped(q) || !string.Equals(q.Groups["name"].Value, name, StringComparison.OrdinalIgnoreCase))
                        continue;
                    var value = RouteAddress.DecodeQuery(uri.Substring(eq + 1, stop - eq - 1));
                    if (TryCanonical(value, q.Groups["type"].Value, out var canonical) && canonical != value)
                        return true;
                    break; // the first one that names it, as FirstOrDefault did
                }
            }
            if (amp < 0)
                break;
            start = amp + 1;
        }
        return false;
    }

    /// <summary>
    /// The address with the values of the typed placeholders (<c>int</c>, <c>long</c>, <c>bool</c>, <c>guid</c>, <c>decimal</c>, <c>double</c>) written the way
    /// <see cref="Build"/> writes them: <c>/x/True</c> is <c>/x/true</c>, <c>/n/007</c> is <c>/n/7</c>. Nothing else of the address changes (a query
    /// the template does not name, the fragment, the other escapes). The address is returned as it is when it already has the canonical text.
    /// </summary>
    /// <param name="template">The template that matched <paramref name="uri"/>.</param>
    /// <param name="uri">The address.</param>
    internal static string Canonical(string template, string uri)
    {
        var parsed = ParseTemplate(template);
        if (!HasTyped(parsed.SegmentMatches) && !HasTyped(parsed.QueryMatches))
            return uri;
        // The usual case: the address is already written as Build writes it. It is read once, without cutting it into pieces, and returned as it is.
        if (!WouldChange(parsed, uri))
            return uri;

        static bool Typed(Match m) => IsTyped(m);

        var hash = uri.IndexOf('#');
        var fragment = hash < 0 ? string.Empty : uri[hash..];
        var body = hash < 0 ? uri : uri[..hash];
        var at = RouteAddress.QueryStart(body);
        var path = at < 0 ? body : body[..at];

        var pieces = path.Split('/');
        var position = 0;
        for (var i = 0; i < pieces.Length && position < parsed.SegmentMatches.Length; i++)
        {
            if (pieces[i].Length == 0)
                continue;
            var m = parsed.SegmentMatches[position++];
            if (!Typed(m) || m.Groups["rest"].Success)
                continue;
            if (Uri.UnescapeDataString(pieces[i]) is var raw && TryCanonical(raw, m.Groups["type"].Value, out var canonical) && canonical != raw)
                pieces[i] = Uri.EscapeDataString(canonical);
        }

        var result = string.Join('/', pieces);
        if (at >= 0)
        {
            var pairs = body[(at + 1)..].Split('&');
            for (var i = 0; i < pairs.Length; i++)
            {
                var eq = pairs[i].IndexOf('=');
                if (eq < 0)
                    continue;
                var name = RouteAddress.DecodeQuery(pairs[i][..eq]);
                var m = parsed.QueryMatches.FirstOrDefault(q => Typed(q) && string.Equals(q.Groups["name"].Value, name, StringComparison.OrdinalIgnoreCase));
                if (m is null)
                    continue;
                var raw = RouteAddress.DecodeQuery(pairs[i][(eq + 1)..]);
                if (TryCanonical(raw, m.Groups["type"].Value, out var canonical) && canonical != raw)
                    pairs[i] = pairs[i][..(eq + 1)] + Uri.EscapeDataString(canonical);
            }
            result += "?" + string.Join('&', pairs);
        }

        return result + fragment;
    }
}

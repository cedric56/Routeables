using Routeables.Abstraction.Routing;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace Routeables.Routing;

/// <summary>
/// Provides route parsing capabilities to match incoming URIs against route templates,
/// extract path and query parameters, convert types, and populate routing arguments.
/// </summary>
public static partial class RouteParser
{

    // {Id:int}  {Tab?}  {Tab::?}  {animate::bool}  {person:Person}  {**path}
    // "::" sets the 'attribute' group, "?" sets 'opt', "**" sets 'rest' (the placeholder takes the rest of the path, slashes included).
    // Generated at build time: no regular expression is built or interpreted at run time, which suits trimmed and AOT applications.
    [GeneratedRegex(@"^\{(?<rest>\*\*)?(?<name>\w+)(?::(?<attribute>:)?(?<type>[^}?]*))?(?<opt>\?)?\}$")]
    private static partial Regex ParamPattern();

    private static readonly Regex Param = ParamPattern();

    // What is read from a template, once. Few templates are registered and each is matched on every navigation; splitting it and matching
    // each segment against Param every time would repeat the same work.
    private sealed class ParsedTemplate
    {
        public required string[] Segments { get; init; }
        public required Match[] SegmentMatches { get; init; } // one per segment; Success is false for a literal
        public required int RestAt { get; init; } // index of the catch-all, or -1
        public required string? OptionalNotLast { get; init; }
        public required string? DuplicateName { get; init; } // a name given by both the path and the query (or twice), or null
        public required string[] QueryParts { get; init; }
        public required Match[] QueryMatches { get; init; }
        public required int[] Rank { get; init; }
    }

    private const int MaxParsedTemplates = 2048; // templates come from the application, so this only bounds a template built from user input
    private static readonly ConcurrentDictionary<string, ParsedTemplate> Parsed = new(StringComparer.Ordinal);

    private static ParsedTemplate ParseTemplate(string template)
    {
        if (Parsed.TryGetValue(template, out var known))
            return known;

        var (path, query) = Split(template);
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var matches = segments.Select(segment => Realized(Param.Match(segment))).ToArray();
        var parts = query.Split('&', StringSplitOptions.RemoveEmptyEntries);
        var parsed = new ParsedTemplate
        {
            Segments = segments,
            SegmentMatches = matches,
            RestAt = Array.FindIndex(matches, found => found.Success && found.Groups["rest"].Success),
            OptionalNotLast = OptionalNotLast(segments),
            QueryParts = parts,
            QueryMatches = parts.Select(part => Realized(Param.Match(part))).ToArray(),
            DuplicateName = DuplicateName(matches, parts),
            Rank = [.. matches.Select(Rank)],
        };

        if (Parsed.Count < MaxParsedTemplates)
            Parsed[template] = parsed;
        return parsed;
    }

    // A Match builds its groups the first time they are read, which is not thread-safe. Read them all here, before the match is shared,
    // so that later navigations only read.
    private static Match Realized(Match m)
    {
        if (m.Success)
            foreach (var name in (ReadOnlySpan<string>)["rest", "name", "attribute", "type", "opt"])
                _ = m.Groups[name];
        return m;
    }

    private static int Rank(Match m)
    {
        if (!m.Success) return 5; // literal
        if (m.Groups["rest"].Success) return 0; // catch-all
        var type = m.Groups["type"].Value;
        var untyped = type is "" || type.Equals("string", StringComparison.OrdinalIgnoreCase); // {id:String} reads like {id}
        if (m.Groups["opt"].Success) return untyped ? 1 : 2; // optional: {id:int?} comes before {tab?}
        return untyped ? 3 : 4; // untyped, then typed
    }

    /// <summary>
    /// Attempts to match a given URI against a route template, extracting parameters and populating arguments.
    /// </summary>
    /// <param name="template">The route template pattern containing literal segments and parameter placeholders (e.g., "users/{id:int}").</param>
    /// <param name="uri">The actual URI or route string being routed to.</param>
    /// <param name="args">Optional existing <see cref="IRouteableArgs"/> instance to populate; a new one is created if null.</param>
    /// <param name="viewKeys">Keys designated specifically to route parameters into view parameter bags rather than view model parameter bags.</param>
    /// <returns><see langword="true"/> if the URI successfully matches the template; otherwise, <see langword="false"/>.</returns>
    internal static bool TryMatch(
        string template, string uri,
        IRouteableArgs? args = null,
        params string[] viewKeys)
        => Check(template, uri, args, viewKeys, reasons: false) is null;

    /// <summary>
    /// Like <see cref="TryMatch"/>, but says why the route does not fit the template.
    /// </summary>
    /// <returns><see langword="null"/> when the route matches; otherwise the reason (extra segment, wrong literal, missing or invalid parameter).</returns>
    internal static string? Mismatch(
        string template, string uri,
        IRouteableArgs? args = null,
        params string[] viewKeys)
        => Check(template, uri, args ?? RouteableArgs.Create(), viewKeys, reasons: true);

    // A mismatch without a reason (reasons is false: the answer is only yes or no) is this text, so no message is built and, without
    // args, no set of bound names either. BestMatch asks this of every candidate and keeps only the one it picks.
    private const string NoReason = "";

    private static string? Check(
        string template, string uri,
        IRouteableArgs? args, string[] viewKeys, bool reasons)
    {
        HashSet<string>? viewKeySet = null, bound = null;
        // Sets are built only when there is something to put in them: usually no view key is given, and the names bound by the path are
        // only read against the query, which many addresses do not have.
        if (args is not null && viewKeys.Any(k => !string.IsNullOrWhiteSpace(k)))
            viewKeySet = new HashSet<string>(
                viewKeys.Where(k => !string.IsNullOrWhiteSpace(k)),
                StringComparer.OrdinalIgnoreCase);

        // The fragment is for the browser, never for the route: "/orders/42#top" is "/orders/42".
        if (uri.IndexOf('#') is var hash and >= 0)
            uri = uri[..hash];

        var parsed = ParseTemplate(template);
        var (uPath, uQuery) = Split(uri, template: false);
        if (args is not null && uQuery.Length > 0)
            bound = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // --- path ---
        var tSegs = parsed.Segments;
        var uSegs = uPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var restAt = parsed.RestAt;
        if (restAt >= 0 && restAt != tSegs.Length - 1)
            return reasons ? $"the catch-all placeholder {tSegs[restAt]} must be the last segment of the template" : NoReason;
        if (parsed.OptionalNotLast is { } notLast)
            return reasons ? $"the optional placeholder {notLast} must be the last segment of the template" : NoReason;
        if (parsed.DuplicateName is { } twice)
            return reasons ? $"the name {twice} is used twice in the template (path and query, or twice in one of them)" : NoReason;
        if (restAt < 0 && uSegs.Length > tSegs.Length)
            return reasons ? $"the route has {uSegs.Length} segment(s), the template only {tSegs.Length} (unexpected \"{uSegs[tSegs.Length]}\")" : NoReason;

        for (var i = 0; i < tSegs.Length; i++)
        {
            var m = parsed.SegmentMatches[i];

            // {**path}: everything that is left, as it was written (each segment unescaped, the slashes kept). Nothing left is no value.
            if (m.Success && m.Groups["rest"].Success)
            {
                // A slash written %2F is not a separator in RouteAddress ("/files/a%2Fb" and "/files/a/b" are two pages), and the value could not tell
                // them apart: only the slashes between the segments are read.
                var pieces = uSegs.Skip(i).Select(Uri.UnescapeDataString).ToArray();
                if (pieces.Any(piece => piece.Contains('/')))
                    return reasons ? "an escaped slash (%2F) is not allowed in a catch-all value" : NoReason;
                var rest = string.Join('/', pieces);
                if (HasDotSegment(rest))
                    return reasons ? $"\"{rest}\" has a \".\" or \"..\" segment, which a browser resolves to another address" : NoReason;
                if (Bind(m, rest, args, viewKeySet, bound, reasons) is { } restFailure) return restFailure;
                break;
            }

            var raw = i < uSegs.Length ? Uri.UnescapeDataString(uSegs[i]) : null;

            // "." and ".." are not values: a browser resolves them (/users/../edit is /edit, even written %2E%2E), so the address shown would differ from the one routed.
            if (m.Success && HasDotSegment(raw))
                return reasons ? $"\"{raw}\" is a \".\" or \"..\" segment, which a browser resolves to another address" : NoReason;

            if (!m.Success) // literal segment
            {
                // The template literal is compared as the address is, unescaped ("/my%20page" is the segment "my page", and Build writes it unchanged).
                var literal = tSegs[i].Contains('%') ? Uri.UnescapeDataString(tSegs[i]) : tSegs[i];
                if (!string.Equals(literal, raw, StringComparison.OrdinalIgnoreCase))
                    return !reasons ? NoReason
                        : raw is null
                            ? $"segment {i + 1} is missing, expected \"{tSegs[i]}\""
                            : $"segment {i + 1} is \"{raw}\", expected \"{tSegs[i]}\"";
                continue;
            }
            if (Bind(m, raw, args, viewKeySet, bound, reasons) is { } failure) return failure;
        }

        // --- query ---
        var query = uQuery.Length == 0 ? NoQuery : ParseQuery(uQuery); // only read below
        for (var q = 0; q < parsed.QueryParts.Length; q++)
        {
            var part = parsed.QueryParts[q];
            var m = parsed.QueryMatches[q];
            if (!m.Success)
            {
                // A fixed entry of the template ("mode=grid") must be in the address as written; a bare "flag" only names a parameter.
                if (part.IndexOf('=') is var eq and > 0)
                {
                    // Read as the address is (a plus is a space, an escape is its character), so that what Build writes reads back.
                    var (fixedName, fixedValue) = (DecodeQuery(part[..eq]), DecodeQuery(part[(eq + 1)..]));
                    query.TryGetValue(fixedName, out var got);
                    if (!string.Equals(got, fixedValue, StringComparison.Ordinal)) // the case of a value counts, as in RouteAddress
                        return reasons ? $"the query \"{fixedName}\" is \"{got}\", expected \"{fixedValue}\"" : NoReason;
                }
                continue;
            }
            query.TryGetValue(m.Groups["name"].Value, out var raw);
            if (Bind(m, raw, args, viewKeySet, bound, reasons) is { } failure) return failure;
        }

        // Query values the template does not declare are neutral values (IRouteableArgs.Values, read by the lifecycle hooks). They are not passed
        // to the constructor or injected into a property: the address is user input, and letting it choose what the page is built with would
        // break every link that carries a tracking parameter and let anyone fill a constructor parameter.
        if (args is not null)
            foreach (var (name, value) in query)
                if (bound is null || !bound.Contains(name))
                    SetValue(args.Values, new RouteableArgument(value, name));

        return null;
    }

    /// <summary>
    /// Binds a matched parameter pattern to its converted value and registers it in the arguments collection.
    /// </summary>
    private static string? Bind(Match m, string? raw, IRouteableArgs? args,
                     HashSet<string>? viewKeySet, HashSet<string>? bound, bool reasons)
    {
        var name = m.Groups["name"].Value;
        var type = m.Groups["type"].Value;
        var attribute = m.Groups["attribute"].Success;
        var optional = m.Groups["opt"].Success || m.Groups["rest"].Success; // a catch-all may be empty

        if (!IsBuiltIn(type) && AmbiguousNames.ContainsKey(type))
            return reasons ? $"the constraint \"{type}\" of {{{name}}} stands for several registered types: write the full name of the type" : NoReason;

        // A constraint that is neither built in nor registered is a mistake in the template: it never matches, and says why (instead of throwing
        // while the router looks for a route).
        if (!IsKnownConstraint(type))
            return reasons ? $"the constraint \"{type}\" of {{{name}}} is unknown (built in: {string.Join(", ", BuiltInConstraints)}; or register a serializer with RouteParser.RegisterSerializer)" : NoReason;

        if (string.IsNullOrEmpty(raw))
        {
            // A declared parameter sent empty ("?q=") is still the declared one, not an undeclared neutral value.
            if (args is not null)
                bound?.Add(name);
            // A missing optional value is skipped; a missing required one is a mismatch.
            return optional ? null : reasons ? $"the required parameter {{{name}}} is missing" : NoReason;
        }

        if (!TryConvert(raw, type, out var value))
            return reasons ? $"\"{raw}\" is not a valid {(type.Length == 0 ? "value" : type)} for {{{name}}}" : NoReason;

        if (args is not null)
        {
            bound?.Add(name);
            Add(args, viewKeySet, name, value, attribute);
        }
        return null;
    }

    /// <summary>
    /// Adds an entry to either the View or ViewModel parameter bags depending on whether the key matches a view key.
    /// </summary>
    private static void Add(IRouteableArgs args, HashSet<string>? viewKeySet,
                    string name, object? value, bool? attribute)
    {
        // A value in the address replaces the caller's value of the same name (the address is the route being shown), so that the constructor,
        // which takes the first value of a name, and the property, which takes the last, read the same one.
        if (viewKeySet?.Contains(name) == true)
        {
            // A view key is for a property of the view (see viewKeys), never for a constructor argument.
            args.ViewParameters.RemoveAll(e => string.Equals(e.name, name, StringComparison.OrdinalIgnoreCase));
            args.ForView(new RouteableArgument(value, name, true));
        }
        else
        {
            args.ViewModelParameters.RemoveAll(e => string.Equals(e.name, name, StringComparison.OrdinalIgnoreCase));
            args.ForViewModel(new RouteableArgument(value, name, attribute ?? false));
        }
    }

    // The first optional path placeholder (not a catch-all) followed by a segment that is not optional. Such a template cannot be written back
    // (tabs/{tab?}/end without a tab is tabs/end, which does not match), so it never matches. Optionals at the end (cat/{age:int?}/{nick?})
    // are fine, and so is a catch-all after them.
    private static string? OptionalNotLast(string[] segments)
    {
        for (var i = 0; i < segments.Length - 1; i++)
        {
            if (Param.Match(segments[i]) is not { Success: true } m || !m.Groups["opt"].Success || m.Groups["rest"].Success)
                continue;
            for (var j = i + 1; j < segments.Length; j++)
                if (Param.Match(segments[j]) is not { Success: true } next || !(next.Groups["opt"].Success || next.Groups["rest"].Success))
                    return segments[i];
        }
        return null;
    }

    // "/dup/{id}?{id}": one value would go to both and the query would be lost. Such a template is a mistake: it never matches and cannot be built.
    private static string? DuplicateName(Match[] segments, string[] queryParts)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in segments.Concat(queryParts.Select(part => Param.Match(part))))
            if (m.Success && !seen.Add(m.Groups["name"].Value))
                return m.Groups["name"].Value;
        return null;
    }

    // A named value appears once: adding one replaces the one with the same name (ignoring case), because Get<T> rejects duplicates.
    internal static void SetValue(List<RouteableArgument> values, RouteableArgument entry)
    {
        if (!string.IsNullOrEmpty(entry.name))
            values.RemoveAll(e => string.Equals(e.name, entry.name, StringComparison.OrdinalIgnoreCase));
        values.Add(entry);
    }

    // "a%2F.." counts too: a server or a proxy may decode %2F, and the dot segment is then resolved.
    private static bool HasDotSegment(string? value)
        => value is not null && Array.Exists(value.Split('/'), part => part is "." or "..");

    private static int[] Specificity(string template) => ParseTemplate(template).Rank;

    private static int CompareRanks(int[] a, int[] b)
    {
        for (var i = 0; i < Math.Min(a.Length, b.Length); i++)
            if (a[i] != b[i])
                return a[i].CompareTo(b[i]);

        // Same start: the shorter template is the better match (/products before /products/{tab?}, /files before /files/{**path}).
        return b.Length.CompareTo(a.Length);
    }

    /// <summary>
    /// Splits a route string into path and query sections while ignoring '?' characters nested inside curly braces.
    /// </summary>
    private static (string path, string query) Split(string s, bool template = true)
    {
        // The braces are those of the template's placeholders; an address is user input and may contain a lone "{" or "}" ("/notes/a}b?tab=2").
        if (!template)
        {
            // The rule of RouteAddress, so that the parser and the comparison of addresses cut an address at the same place.
            var at = RouteAddress.QueryStart(s);
            return at < 0 ? (s, "") : (s[..at], s[(at + 1)..]);
        }

        var depth = 0;
        for (var i = 0; i < s.Length; i++)
        {
            if (s[i] == '{') depth++;
            else if (s[i] == '}') depth--;
            else if (s[i] == '?' && depth == 0) return (s[..i], s[(i + 1)..]);
        }
        return (s, "");
    }

    /// <summary>
    /// A piece of a template that looks like a placeholder and is not one (<c>{id?:int}</c> for <c>{id:int?}</c>, <c>q={q}</c> for <c>{q}</c> in a query), which the
    /// parser reads as fixed text: <see langword="null"/> when there is none. Used to say why a registered route does not match.
    /// </summary>
    internal static string? LooksLikeAPlaceholder(string template)
    {
        var (path, query) = Split(template);
        foreach (var segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
            if (segment.Contains('{') && segment.Contains('}') && !Param.IsMatch(segment))
                return segment;

        foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            if (!Param.IsMatch(part) && part.IndexOf('=') is var eq and > 0 && part[(eq + 1)..] is var value && value.StartsWith('{') && value.EndsWith('}'))
                return part;

        return null;
    }

}
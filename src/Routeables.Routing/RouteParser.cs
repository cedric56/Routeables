using Routeables.Abstraction;
using Routeables.Abstraction.Routing;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;

namespace Routeables.Routing;

/// <summary>
/// Provides route parsing capabilities to match incoming URIs against route templates,
/// extract path and query parameters, convert types, and populate routing arguments.
/// </summary>
public static partial class RouteParser
{
    // Serialization information for complex route parameters (System.Text.Json), by type name, case-insensitive.
    // Private: callers register through RegisterSerializer, nobody can replace or empty the table.
    // A type is known by its full name (always) and by its simple name (until another type takes it: the simple name is then ambiguous).
    private static readonly ConcurrentDictionary<string, JsonTypeInfo> SerializationInfos =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, Type> SerializationTypes = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, bool> AmbiguousNames = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object RegistryLock = new();

    /// <summary>
    /// Teaches the parser to read <c>{name:typeName}</c> parameters as <paramref name="info"/>'s type (trim-safe JSON, no reflection).
    /// The registry is global to the process. A type is written in a template by its simple name (<c>{c:Customer}</c>) or its full name
    /// (<c>{c:Sales.Customer}</c>). Registering the same type again replaces its information; when two types share a simple name, the simple
    /// name no longer stands for either of them and only the full names are accepted.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="info"/> describes another type than <typeparamref name="T"/>.</exception>
    public static void RegisterSerializer<T>(JsonTypeInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        if (info.Type != typeof(T))
            throw new ArgumentException($"The JSON information is for {info.Type}, not for {typeof(T)}.", nameof(info));

        lock (RegistryLock)
        {
            var type = typeof(T);
            if (type.FullName is { } full)
            {
                SerializationInfos[full] = info;
                SerializationTypes[full] = type;
            }

            var simple = type.Name;
            if (type.FullName == simple)
            {
                // No namespace: the simple name is the full name, stored above. The type owns it, and a homonym of a namespace is reached by
                // its own full name.
                AmbiguousNames.TryRemove(simple, out _);
                return;
            }

            if (AmbiguousNames.ContainsKey(simple))
                return;
            if (SerializationTypes.TryGetValue(simple, out var owner) && owner != type)
            {
                if (owner.FullName == simple)
                    return;     // the type with no namespace keeps the name that is its full name

                // A second type with this simple name: nobody owns it any more.
                SerializationInfos.TryRemove(simple, out _);
                SerializationTypes.TryRemove(simple, out _);
                AmbiguousNames[simple] = true;
                return;
            }

            SerializationInfos[simple] = info;
            SerializationTypes[simple] = type;
        }
    }

    /// <summary>
    /// Takes one type out of the registry again. For tests that register a type of their own and must leave the process as they found it
    /// (the registry is global, and tests run in parallel: emptying it would take the registrations of the others). A simple name that was
    /// already ambiguous stays so.
    /// </summary>
    internal static void UnregisterSerializer<T>()
    {
        lock (RegistryLock)
        {
            var type = typeof(T);
            if (type.FullName is { } full)
            {
                SerializationInfos.TryRemove(full, out _);
                SerializationTypes.TryRemove(full, out _);
            }

            if (SerializationTypes.TryGetValue(type.Name, out var owner) && owner == type)
            {
                SerializationInfos.TryRemove(type.Name, out _);
                SerializationTypes.TryRemove(type.Name, out _);
            }
        }
    }

    // {Id:int}  {Tab?}  {Tab::?}  {animate::bool}  {person:Person}  {**path}
    // "::" sets the 'attribute' group, "?" sets 'opt', "**" sets 'rest': the placeholder takes what is left of the path, slashes included
    // Generated at build time: no regular expression is built (or interpreted) at run time, which is what a trimmed or AOT application wants.
    [GeneratedRegex(@"^\{(?<rest>\*\*)?(?<name>\w+)(?::(?<attribute>:)?(?<type>[^}?]*))?(?<opt>\?)?\}$")]
    private static partial Regex ParamPattern();

    private static readonly Regex Param = ParamPattern();

    // What is read from a route template, once: the registered templates are few and each one is matched on every navigation, so splitting it
    // and matching each segment against Param again for every route change would be the same work repeated.
    private sealed class ParsedTemplate
    {
        public required string[] Segments { get; init; }
        public required Match[] SegmentMatches { get; init; }      // one per segment, Success false for a literal
        public required int RestAt { get; init; }                  // index of the catch-all, or -1
        public required string? OptionalNotLast { get; init; }
        public required string? DuplicateName { get; init; }       // a name given by the path and by the query (or twice), or null
        public required string[] QueryParts { get; init; }
        public required Match[] QueryMatches { get; init; }
        public required int[] Rank { get; init; }
    }

    private const int MaxParsedTemplates = 2048;                   // the templates are registered by the application: a bound only against a template made of user input
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

    // A Match builds its groups the first time they are read, which is not safe from several threads at once: they are all read here, before the
    // match is shared, so the navigations that read it afterwards only read.
    private static Match Realized(Match m)
    {
        if (m.Success)
            foreach (var name in (ReadOnlySpan<string>)["rest", "name", "attribute", "type", "opt"])
                _ = m.Groups[name];
        return m;
    }

    private static int Rank(Match m)
    {
        if (!m.Success) return 5;                                   // literal
        if (m.Groups["rest"].Success) return 0;                     // catch-all
        var type = m.Groups["type"].Value;
        var untyped = type is "" or "?" || type.Equals("string", StringComparison.OrdinalIgnoreCase);   // {id:String} reads like {id}
        if (m.Groups["opt"].Success) return untyped ? 1 : 2;        // optional: {id:int?} before {tab?}
        return untyped ? 3 : 4;                                     // untyped, typed
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

    // A mismatch without a reason (reasons false, the answer is only yes or no) is this text: no message is built and, without arguments to
    // fill (args null), no set of bound names either. BestMatch asks this question of every candidate and keeps only the one it picks.
    private const string NoReason = "";

    private static string? Check(
        string template, string uri,
        IRouteableArgs? args, string[] viewKeys, bool reasons)
    {
        HashSet<string>? viewKeySet = null, bound = null;
        // Sets are built when there is something to put in them: no view key is the usual case, and the names bound by the path are
        // only read against the query, which an address often does not have.
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
                var rest = string.Join('/', uSegs.Skip(i).Select(Uri.UnescapeDataString));
                if (HasDotSegment(rest))
                    return reasons ? $"\"{rest}\" has a \".\" or \"..\" segment, which a browser resolves to another address" : NoReason;
                if (Bind(m, rest, args, viewKeySet, bound, reasons) is { } restFailure) return restFailure;
                break;
            }

            var raw = i < uSegs.Length ? Uri.UnescapeDataString(uSegs[i]) : null;

            // "." and ".." are not values: a browser resolves them (/users/../edit is /edit, even written %2E%2E), so the address shown would not be the one routed.
            if (m.Success && HasDotSegment(raw))
                return reasons ? $"\"{raw}\" is a \".\" or \"..\" segment, which a browser resolves to another address" : NoReason;

            if (!m.Success) // literal segment
            {
                // The literal of the template is compared as the address is: unescaped ("/my%20page" is the segment "my page", and Build writes it as it was).
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
        var query = uQuery.Length == 0 ? NoQuery : ParseQuery(uQuery);   // read only below
        for (var q = 0; q < parsed.QueryParts.Length; q++)
        {
            var part = parsed.QueryParts[q];
            var m = parsed.QueryMatches[q];
            if (!m.Success)
            {
                // A fixed entry of the template ("mode=grid") must be in the address as written; "flag" alone only names a parameter.
                if (part.IndexOf('=') is var eq and > 0)
                {
                    // Read as the address is (a plus is a space, an escape is its character), so that what Build writes is read back.
                    var (fixedName, fixedValue) = (DecodeQuery(part[..eq]), DecodeQuery(part[(eq + 1)..]));
                    query.TryGetValue(fixedName, out var got);
                    if (!string.Equals(got, fixedValue, StringComparison.OrdinalIgnoreCase))
                        return reasons ? $"the query \"{fixedName}\" is \"{got}\", expected \"{fixedValue}\"" : NoReason;
                }
                continue;
            }
            query.TryGetValue(m.Groups["name"].Value, out var raw);
            if (Bind(m, raw, args, viewKeySet, bound, reasons) is { } failure) return failure;
        }

        // Query values the template does not declare are neutral values (IRouteableArgs.Values, read by the lifecycle hooks): they
        // are not given to the constructor or injected into a property. The address is user input; letting it name what the page is
        // built with would break every link that carries a tracking parameter and let anyone fill a constructor parameter.
        if (args is not null)
            foreach (var (name, value) in query)
                if (bound is null || !bound.Contains(name))
                    args.ForValues(new RouteableArgument(value, name));

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
        var optional = m.Groups["opt"].Success || m.Groups["rest"].Success;   // a catch-all may be empty

        if (!IsBuiltIn(type) && AmbiguousNames.ContainsKey(type))
            return reasons ? $"the constraint \"{type}\" of {{{name}}} stands for several registered types: write the full name of the type" : NoReason;

        // A constraint that is neither built in nor registered is a mistake in the template: it never matches, and says so (instead of throwing
        // while the router looks for a route).
        if (!IsKnownConstraint(type))
            return reasons ? $"the constraint \"{type}\" of {{{name}}} is unknown (built in: {string.Join(", ", BuiltInConstraints)}; or register a serializer with RouteParser.RegisterSerializer)" : NoReason;

        if (string.IsNullOrEmpty(raw))
        {
            // a missing optional value is skipped, a missing required one is a mismatch
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
        if (viewKeySet?.Contains(name) == true)
            args.ForView(new RouteableArgument(value, name, attribute ?? true));
        else
            args.ForViewModel(new RouteableArgument(value, name, attribute ?? false));
    }

    // The first optional path placeholder (not a catch-all) that is followed by a segment that is not optional itself: such a template cannot
    // be written back (tabs/{tab?}/end without a tab is tabs/end, which does not match), so it never matches. Optionals one after the other
    // at the end (cat/{age:int?}/{nick?}) are fine, and so is a catch-all after them.
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

    // "/dup/{id}?{id}": one value would be given to both and the query would be lost. Such a template is a mistake: it never matches and cannot be built.
    private static string? DuplicateName(Match[] segments, string[] queryParts)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in segments.Concat(queryParts.Select(part => Param.Match(part))))
            if (m.Success && !seen.Add(m.Groups["name"].Value))
                return m.Groups["name"].Value;
        return null;
    }

    private static bool HasDotSegment(string? value)
        => value is not null && Array.Exists(value.Split('/'), part => part is "." or "..");

    private static readonly string[] BuiltInConstraints = ["string", "int", "long", "bool", "guid", "decimal", "double"];

    private static bool IsBuiltIn(string type)
        => type is "" or "?" || Array.Exists(BuiltInConstraints, c => string.Equals(c, type, StringComparison.OrdinalIgnoreCase));

    // The registered type of a constraint. A built-in name is never a registered type: a type called Guid, String or Double that the
    // application registers must not turn {id:guid} into a JSON value (it is reached by its full name, {id:Sales.Guid}).
    private static bool TryGetSerializer(string type, out JsonTypeInfo info)
    {
        if (IsBuiltIn(type)) { info = null!; return false; }
        return SerializationInfos.TryGetValue(type, out info!);
    }

    private static bool IsKnownConstraint(string type)
        => IsBuiltIn(type) || SerializationInfos.ContainsKey(type);

    /// <summary>
    /// Converts a raw string parameter value into the specified target type constraint.
    /// </summary>
    private static bool TryConvert(string raw, string type, out object? value)
    {
        value = null;

        if (TryGetSerializer(type, out var info))
        {
            try { value = JsonSerializer.Deserialize(raw, info); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Not only a JsonException: a converter of the application, a type the information cannot read... An address that cannot be read as
                // the type is an address that does not match, not an exception out of the search for a route (it runs for every address).
                return false;
            }
            return value is not null;
        }

        var inv = CultureInfo.InvariantCulture;
        switch (type.ToLowerInvariant())
        {
            case "" or "string" or "?": value = raw; return true;
            // A value has one text. A number: the one that ToString writes ("42", not "+42", " 42" or "007"); a Guid: with its dashes and
            // without braces; a bool: with no space around it.
            case "int": { int v = 0; var ok = int.TryParse(raw, NumberStyles.AllowLeadingSign, inv, out v) && v.ToString(inv) == raw; value = v; return ok; }
            case "long": { long v = 0; var ok = long.TryParse(raw, NumberStyles.AllowLeadingSign, inv, out v) && v.ToString(inv) == raw; value = v; return ok; }
            case "bool": { var ok = raw == raw.Trim() && bool.TryParse(raw, out var v); value = ok && bool.Parse(raw); return ok; }
            case "guid": { var ok = Guid.TryParseExact(raw, "D", out var v); value = v; return ok; }
            case "decimal": { decimal v = 0; var ok = !raw.StartsWith('+') && decimal.TryParse(raw, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, inv, out v) && Plain(v).ToString(inv) == raw; value = v; return ok; }
            case "double": { double v = 0; var ok = !raw.StartsWith('+') && double.TryParse(raw, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent, inv, out v) && double.IsFinite(v) && !(v == 0 && raw[0] == '-') && v.ToString(inv) == raw; value = v; return ok; }
            default:
                value = null;
                return false;   // not reached: Bind refuses an unknown constraint first
        }
    }

    /// <summary>
    /// The registered route that opens <paramref name="uri"/>. When several templates match, the most specific wins, whatever their
    /// registration order: segment by segment from the left, a literal beats a typed parameter (<c>{id:int}</c>, a registered type),
    /// which beats an untyped one (<c>{id}</c>), which beats an optional one (<c>{tab?}</c>). <c>/bears/new</c> is therefore
    /// <c>bears/new</c>, not <c>bears/{id}</c>. When one template is the start of the other, the shorter wins (<c>/products</c> over
    /// <c>/products/{tab?}</c>). Equally specific templates keep the order of registration.
    /// </summary>
    /// <returns>The entry, or one whose <c>Template</c> is <see langword="null"/> when no template matches.</returns>
    internal static (string Template, Type? ViewModelType) BestMatch(
        IEnumerable<(string Template, Type? ViewModelType)> entries, string uri)
    {
        var best = default((string Template, Type? ViewModelType));
        int[]? bestRank = null;
        int? bestQuery = null;

        void Consider((string Template, Type? ViewModelType) entry)
        {
            if (!TryMatch(entry.Template, uri))
                return;

            var rank = Specificity(entry.Template);
            var order = bestRank is null ? 1 : CompareRanks(rank, bestRank);
            var queryScore = 0;
            if (order == 0)
            {
                // The same path: the one that uses more of the query of the address is the more exact (/search?{q} over /search for /search?q=1).
                queryScore = QueryScore(entry.Template, uri);
                bestQuery ??= QueryScore(best.Template!, uri);
                if (queryScore > bestQuery)
                    order = 1;
            }

            if (order > 0)
            {
                best = entry;
                bestRank = rank;
                bestQuery = order == 1 && queryScore > 0 ? queryScore : null;
            }
        }

        // The table of the application never changes once built (an add makes a new collection): it is indexed by the first literal
        // segment, and only the templates that can open the address are tried, in the order they were registered (the order breaks a tie).
        if (entries is ReadOnlyCollection<(string Template, Type? ViewModelType)> table && table.Count >= IndexFrom)
        {
            var index = Indexes.GetValue(table, t => new FirstSegmentIndex(t));
            if (index.Count != table.Count)
                index = new FirstSegmentIndex(table);   // a collection that was changed behind our back: the index is not trusted

            foreach (var at in index.Candidates(FirstSegment(uri)))
                Consider(table[at]);
            return best;
        }

        foreach (var entry in entries)
            Consider(entry);
        return best;
    }

    // How many entries of the query of the address a template uses: its fixed entries ("mode=grid") and its placeholders the address gives a value to.
    private static int QueryScore(string template, string uri)
    {
        var parsed = ParseTemplate(template);
        if (parsed.QueryParts.Length == 0)
            return 0;

        if (uri.IndexOf('#') is var hash and >= 0)
            uri = uri[..hash];
        var (_, uQuery) = Split(uri, template: false);
        var query = uQuery.Length == 0 ? NoQuery : ParseQuery(uQuery);
        var score = 0;
        for (var q = 0; q < parsed.QueryParts.Length; q++)
        {
            var m = parsed.QueryMatches[q];
            if (!m.Success)
            {
                if (parsed.QueryParts[q].IndexOf('=') > 0)
                    score++;
            }
            else if (query.TryGetValue(m.Groups["name"].Value, out var raw) && !string.IsNullOrEmpty(raw))
                score++;
        }
        return score;
    }

    private const int IndexFrom = 8;                                // below this, trying every template is as quick as looking one up
    private static readonly ConditionalWeakTable<ReadOnlyCollection<(string Template, Type? ViewModelType)>, FirstSegmentIndex> Indexes = new();

    /// <summary>The first segment of an address as the matching reads it: the fragment and the query left out, empty segments skipped, unescaped.</summary>
    private static string FirstSegment(string uri)
    {
        var end = uri.AsSpan().IndexOfAny('?', '#');
        var path = end < 0 ? uri.AsSpan() : uri.AsSpan(0, end);
        path = path.TrimStart('/');
        var slash = path.IndexOf('/');
        var first = slash < 0 ? path : path[..slash];
        return first.IndexOf('%') < 0 ? first.ToString() : Uri.UnescapeDataString(first.ToString());
    }

    /// <summary>
    /// The registered templates by their first segment: the ones that start with a literal under that literal, the ones that start
    /// with a placeholder (or have no segment: "/") apart, because they can open any address.
    /// </summary>
    private sealed class FirstSegmentIndex
    {
        private readonly Dictionary<string, int[]> _byLiteral = new(StringComparer.OrdinalIgnoreCase);
        private readonly int[] _open;
        public int Count { get; }

        public FirstSegmentIndex(ReadOnlyCollection<(string Template, Type? ViewModelType)> table)
        {
            Count = table.Count;
            var literal = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
            var open = new List<int>();
            for (var i = 0; i < table.Count; i++)
            {
                var parsed = ParseTemplate(table[i].Template);
                if (parsed.Segments.Length > 0 && !parsed.SegmentMatches[0].Success)
                {
                    // The address is looked up unescaped (FirstSegment), so the literal is too ("my%20page" is "my page").
                    var first = parsed.Segments[0].Contains('%') ? Uri.UnescapeDataString(parsed.Segments[0]) : parsed.Segments[0];
                    if (!literal.TryGetValue(first, out var list))
                        literal[first] = list = [];
                    list.Add(i);
                }
                else
                    open.Add(i);
            }
            foreach (var (key, list) in literal)
                _byLiteral[key] = [.. list];
            _open = [.. open];
        }

        /// <summary>The positions of the templates that can match an address starting with <paramref name="first"/>, in registration order.</summary>
        public IEnumerable<int> Candidates(string first)
        {
            if (first.Length == 0 || !_byLiteral.TryGetValue(first, out var literal))
                return _open;
            if (_open.Length == 0)
                return literal;

            // both lists are in ascending order: the merge keeps the order of registration
            var merged = new int[literal.Length + _open.Length];
            int a = 0, b = 0, k = 0;
            while (a < literal.Length && b < _open.Length)
                merged[k++] = literal[a] < _open[b] ? literal[a++] : _open[b++];
            while (a < literal.Length) merged[k++] = literal[a++];
            while (b < _open.Length) merged[k++] = _open[b++];
            return merged;
        }
    }

    private static int[] Specificity(string template) => ParseTemplate(template).Rank;

    private static int CompareRanks(int[] a, int[] b)
    {
        for (var i = 0; i < Math.Min(a.Length, b.Length); i++)
            if (a[i] != b[i])
                return a[i].CompareTo(b[i]);

        // The same start: the shorter template is the more exact one (/products before /products/{tab?}, /files before /files/{**path}).
        return b.Length.CompareTo(a.Length);
    }

    /// <summary>
    /// Splits a route string into path and query sections while ignoring '?' characters nested inside curly braces.
    /// </summary>
    private static (string path, string query) Split(string s, bool template = true)
    {
        // The braces are those of a template's placeholders; an address is user input and may hold a lone "{" or "}" ("/notes/a}b?tab=2").
        if (!template)
        {
            var at = s.IndexOf('?');
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

    /// <summary>
    /// Writes the route a template stands for: the placeholders are filled from <paramref name="values"/> (an object's properties or a
    /// dictionary of name to value; names are not case-sensitive). Numbers and dates are written with the invariant culture, a
    /// registered type (<c>{person:Person}</c>) as JSON, and each value is escaped. A missing optional value leaves its segment (or its
    /// query entry) out.
    /// </summary>
    /// <exception cref="ArgumentException">A required value is missing, or a constraint is unknown.</exception>
    internal static string Build(string template, object? values)
    {
        ArgumentNullException.ThrowIfNull(template);
        var bag = ValuesOf(values);
        if (ParseTemplate(template).DuplicateName is { } twice)
            throw new ArgumentException($"The name {twice} is used twice in \"{template}\": a value cannot be written in the path and in the query.", nameof(template));
        if (OptionalNotLast(Split(template).path.Split('/', StringSplitOptions.RemoveEmptyEntries)) is { } notLast)
            throw new ArgumentException($"The optional placeholder {notLast} of \"{template}\" must be the last segment of the template.", nameof(template));

        string? Fill(Match m, string where)
        {
            var name = m.Groups["name"].Value;
            var type = m.Groups["type"].Value;
            if (!IsKnownConstraint(type))
                throw new ArgumentException($"The constraint \"{type}\" of {{{name}}} in \"{template}\" is unknown.", nameof(template));
            if (!bag.TryGetValue(name, out var value) || value is null || value is string { Length: 0 })
            {
                if (m.Groups["opt"].Success) return null;
                throw new ArgumentException($"The route \"{template}\" needs a value for {{{name}}} ({where}).", nameof(values));
            }

            var text = Format(value, type);
            if (where == "path" && HasDotSegment(text))
                throw new ArgumentException($"The value \"{text}\" cannot be a segment of \"{template}\" for {{{name}}}: a browser resolves \".\" and \"..\" to another address.", nameof(values));
            // What is written must be read back: the router would refuse a route that its own template does not accept.
            if (!TryConvert(text, type, out _))
                throw new ArgumentException(
                    $"The value \"{text}\" is not a valid {(type.Length == 0 ? "value" : type)} for {{{name}}} in \"{template}\".", nameof(values));
            return Uri.EscapeDataString(text);
        }

        var (path, query) = Split(template);
        var segments = new List<string>();
        string? skippedOptional = null;
        foreach (var segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            var m = Param.Match(segment);
            if (!m.Success) { segments.Add(segment); continue; }
            if (m.Groups["rest"].Success)
            {
                // {**path}: the slashes of the value stay, each segment is escaped.
                if (bag.TryGetValue(m.Groups["name"].Value, out var restValue) && restValue is not null
                    && Format(restValue, m.Groups["type"].Value) is { Length: > 0 } restText)
                {
                    // What is written must be read back, as for the other placeholders: the whole value is checked against the constraint.
                    var restType = m.Groups["type"].Value;
                    if (!IsKnownConstraint(restType))
                        throw new ArgumentException($"The constraint \"{restType}\" of {{**{m.Groups["name"].Value}}} in \"{template}\" is unknown.", nameof(template));
                    if (restText.Split('/').Any(part => part.Length == 0))
                        throw new ArgumentException($"The value \"{restText}\" cannot be the rest of \"{template}\": an empty segment (a leading, trailing or doubled slash) is dropped from an address, so the value would not be read back.", nameof(values));
                    if (HasDotSegment(restText))
                        throw new ArgumentException($"The value \"{restText}\" cannot be the rest of \"{template}\": a browser resolves \".\" and \"..\" to another address.", nameof(values));
                    if (!TryConvert(restText, restType, out _))
                        throw new ArgumentException(
                            $"The value \"{restText}\" is not a valid {(restType.Length == 0 ? "value" : restType)} for {{**{m.Groups["name"].Value}}} in \"{template}\".", nameof(values));
                    // cat/{age?}/{**rest} with a rest and no age: the rest would be read as the age.
                    if (skippedOptional is { } skippedBefore)
                        throw new ArgumentException($"The route \"{template}\" cannot leave out {skippedBefore} and give a later value: give both, or neither.", nameof(values));
                    segments.AddRange(restText.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString));
                }
                continue;
            }
            if (Fill(m, "path") is { } text)
            {
                // cat/{age?}/{nick?} with a nick and no age would be read as the age of the cat.
                if (skippedOptional is { } skipped)
                    throw new ArgumentException($"The route \"{template}\" cannot leave out {skipped} and give a later value: give both, or neither.", nameof(values));
                segments.Add(text);
            }
            else if (m.Groups["opt"].Success)
            {
                skippedOptional ??= segment;
            }
        }

        var pairs = new List<string>();
        foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var m = Param.Match(part);
            if (!m.Success) { pairs.Add(part); continue; }
            if (Fill(m, "query") is { } text) pairs.Add($"{Uri.EscapeDataString(m.Groups["name"].Value)}={text}");
        }

        // A template that is only optional values, with none given ("{tab?}"), is the root.
        var route = (path.StartsWith('/') || segments.Count == 0 ? "/" : string.Empty) + string.Join('/', segments);
        return pairs.Count == 0 ? route : route + "?" + string.Join('&', pairs);
    }

    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Reached with an object of values only through the overload of GoToAsync that is marked RequiresUnreferencedCode.")]
    private static Dictionary<string, object?> ValuesOf(object? values)
    {
        var bag = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        switch (values)
        {
            case null:
                break;
            case IReadOnlyDictionary<string, object?> map:
                foreach (var (key, value) in map) bag[key] = value;
                break;
            case IDictionary<string, object?> map:
                foreach (var (key, value) in map) bag[key] = value;
                break;
            case System.Collections.IDictionary map when map.GetType().IsGenericType && map.GetType().GetGenericArguments() is [var key, _] && key == typeof(string):
                // Dictionary<string, int>, Dictionary<string, string>...: the generic interfaces are not covariant, so none of the cases above takes them.
                foreach (System.Collections.DictionaryEntry entry in map) bag[(string)entry.Key] = entry.Value;
                break;
            default:
                foreach (var property in values.GetType().GetProperties())
                    if (property.GetIndexParameters().Length == 0 && property.CanRead)
                        bag[property.Name] = property.GetValue(values);
                break;
        }

        return bag;
    }

    // A decimal keeps the zeros it was written with (1.5m, 1.50m and 1.0m are one number): the text is the one without them, so a number has one address.
    private static decimal Plain(decimal value) => value / 1.0000000000000000000000000000m;

    private static string Format(object value, string type)
    {
        if (TryGetSerializer(type, out var info))
            return JsonSerializer.Serialize(value, info);
        return value switch
        {
            string text => text,
            bool flag => flag ? "true" : "false",
            double zero when zero == 0 => "0",     // -0 is the number 0: one text
            decimal money => Plain(money).ToString(CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty,
        };
    }

    /// <summary>The query of an address as name and value (the fragment is left out): the values a page that has no template receives.</summary>
    internal static IReadOnlyDictionary<string, string> QueryOf(string url)
    {
        if (url.IndexOf('#') is var hash and >= 0)
            url = url[..hash];
        return ParseQuery(Split(url, template: false).query);
    }

    private static readonly Dictionary<string, string> NoQuery = new(StringComparer.OrdinalIgnoreCase);

    private static Dictionary<string, string> ParseQuery(string q)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in q.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var p = pair.Split('=', 2);
            result[DecodeQuery(p[0])] =
                p.Length > 1 ? DecodeQuery(p[1]) : "";   // last value wins on duplicates
        }
        return result;
    }

    /// <summary>
    /// A name or a value of a query string: <c>+</c> is a space and <c>%XX</c> is a character, as ASP.NET Core and Blazor read it (a form sent with
    /// GET writes a space as <c>+</c>). A real plus is <c>%2B</c>, which is what <see cref="Uri.EscapeDataString(string)"/> writes. A path segment is
    /// not a query: <c>+</c> stays a plus there.
    /// </summary>
    internal static string DecodeQuery(string text) => Uri.UnescapeDataString(text.Replace('+', ' '));
}
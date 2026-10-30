using Routeables.Abstraction;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;

namespace Routeables.Routing;

/// <summary>
/// Provides route parsing capabilities to match incoming URIs against route templates,
/// extract path and query parameters, convert types, and populate routing arguments.
/// </summary>
public static class RouteParser
{
    // Serialization information for complex route parameters (System.Text.Json), by type name, case-insensitive.
    // Private: callers register through RegisterSerializer, nobody can replace or empty the table.
    private static readonly ConcurrentDictionary<string, JsonTypeInfo> SerializationInfos =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Teaches the parser to read <c>{name:typeName}</c> parameters as <paramref name="info"/>'s type (trim-safe JSON, no reflection).
    /// A later registration under the same name replaces the earlier one.
    /// </summary>
    public static void RegisterSerializer<T>(JsonTypeInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        SerializationInfos[typeof(T).Name] = info;
    }

    // {Id:int}  {Tab?}  {Tab::?}  {animate::bool}  {person:Person}
    // "::" sets the 'attribute' group, "?" sets 'opt'
    private static readonly Regex Param =
        new(@"^\{(?<name>\w+)(?::(?<attribute>:)?(?<type>[^}?]*))?(?<opt>\?)?\}$");

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
        => Mismatch(template, uri, args, viewKeys) is null;

    /// <summary>
    /// Like <see cref="TryMatch"/>, but says why the route does not fit the template.
    /// </summary>
    /// <returns><see langword="null"/> when the route matches; otherwise the reason (extra segment, wrong literal, missing or invalid parameter).</returns>
    internal static string? Mismatch(
        string template, string uri,
        IRouteableArgs? args = null,
        params string[] viewKeys)
    {
        args ??= RouteableArgs.Create();

        var viewKeySet = new HashSet<string>(
            viewKeys.Where(k => !string.IsNullOrWhiteSpace(k)),
            StringComparer.OrdinalIgnoreCase);
        var bound = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var (tPath, tQuery) = Split(template);
        var (uPath, uQuery) = Split(uri);

        // --- path ---
        var tSegs = tPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var uSegs = uPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (uSegs.Length > tSegs.Length)
            return $"the route has {uSegs.Length} segment(s), the template only {tSegs.Length} (unexpected \"{uSegs[tSegs.Length]}\")";

        for (var i = 0; i < tSegs.Length; i++)
        {
            var raw = i < uSegs.Length ? Uri.UnescapeDataString(uSegs[i]) : null;
            var m = Param.Match(tSegs[i]);

            if (!m.Success) // literal segment
            {
                if (!string.Equals(tSegs[i], raw, StringComparison.OrdinalIgnoreCase))
                    return raw is null
                        ? $"segment {i + 1} is missing, expected \"{tSegs[i]}\""
                        : $"segment {i + 1} is \"{raw}\", expected \"{tSegs[i]}\"";
                continue;
            }
            if (Bind(m, raw, args, viewKeySet, bound) is { } failure) return failure;
        }

        // --- query ---
        var query = ParseQuery(uQuery);
        foreach (var part in tQuery.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var m = Param.Match(part);
            if (!m.Success) continue;
            query.TryGetValue(m.Groups["name"].Value, out var raw);
            if (Bind(m, raw, args, viewKeySet, bound) is { } failure) return failure;
        }

        // Query values the template does not declare are neutral values (IRouteableArgs.Values, read by the lifecycle hooks): they
        // are not given to the constructor or injected into a property. The address is user input; letting it name what the page is
        // built with would break every link that carries a tracking parameter and let anyone fill a constructor parameter.
        foreach (var (name, value) in query)
            if (!bound.Contains(name))
                args.ForValues(new RouteableArgument(value, name));

        return null;
    }

    /// <summary>
    /// Binds a matched parameter pattern to its converted value and registers it in the arguments collection.
    /// </summary>
    private static string? Bind(Match m, string? raw, IRouteableArgs args,
                     HashSet<string> viewKeySet, HashSet<string> bound)
    {
        var name = m.Groups["name"].Value;
        var type = m.Groups["type"].Value;
        var attribute = m.Groups["attribute"].Success;
        var optional = m.Groups["opt"].Success;

        // A constraint that is neither built in nor registered is a mistake in the template: it never matches, and says so (instead of throwing
        // while the router looks for a route).
        if (!IsKnownConstraint(type))
            return $"the constraint \"{type}\" of {{{name}}} is unknown (built in: {string.Join(", ", BuiltInConstraints)}; or register a serializer with RouteParser.RegisterSerializer)";

        if (string.IsNullOrEmpty(raw))
        {
            // a missing optional value is skipped, a missing required one is a mismatch
            return optional ? null : $"the required parameter {{{name}}} is missing";
        }

        if (!TryConvert(raw, type, out var value))
            return $"\"{raw}\" is not a valid {(type.Length == 0 ? "value" : type)} for {{{name}}}";

        bound.Add(name);
        Add(args, viewKeySet, name, value, attribute);
        return null;
    }

    /// <summary>
    /// Adds an entry to either the View or ViewModel parameter bags depending on whether the key matches a view key.
    /// </summary>
    private static void Add(IRouteableArgs args, HashSet<string> viewKeySet,
                    string name, object? value, bool? attribute)
    {
        if (viewKeySet.Contains(name))
            args.ForView(new RouteableArgument(value, name, attribute ?? true));
        else
            args.ForViewModel(new RouteableArgument(value, name, attribute ?? false));
    }

    private static readonly string[] BuiltInConstraints = ["string", "int", "long", "bool", "guid", "decimal", "double"];

    private static bool IsKnownConstraint(string type)
        => type is "" or "?"
           || SerializationInfos.ContainsKey(type)
           || Array.Exists(BuiltInConstraints, c => string.Equals(c, type, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Converts a raw string parameter value into the specified target type constraint.
    /// </summary>
    private static bool TryConvert(string raw, string type, out object? value)
    {
        value = null;

        if (SerializationInfos.TryGetValue(type, out var info))
        {
            try { value = JsonSerializer.Deserialize(raw, info); }
            catch (JsonException) { return false; }
            return value is not null;
        }

        var inv = CultureInfo.InvariantCulture;
        switch (type.ToLowerInvariant())
        {
            case "" or "string" or "?": value = raw; return true;
            case "int": { var ok = int.TryParse(raw, NumberStyles.Integer, inv, out var v); value = v; return ok; }
            case "long": { var ok = long.TryParse(raw, NumberStyles.Integer, inv, out var v); value = v; return ok; }
            case "bool": { var ok = bool.TryParse(raw, out var v); value = v; return ok; }
            case "guid": { var ok = Guid.TryParse(raw, out var v); value = v; return ok; }
            case "decimal": { var ok = decimal.TryParse(raw, NumberStyles.Number, inv, out var v); value = v; return ok; }
            case "double": { var ok = double.TryParse(raw, NumberStyles.Float, inv, out var v); value = v; return ok; }
            default:
                value = null;
                return false;   // not reached: Bind refuses an unknown constraint first
        }
    }

    /// <summary>
    /// The registered route that opens <paramref name="uri"/>. When several templates match, the most specific wins, whatever their
    /// registration order: segment by segment from the left, a literal beats a typed parameter (<c>{id:int}</c>, a registered type),
    /// which beats an untyped one (<c>{id}</c>), which beats an optional one (<c>{tab?}</c>). <c>/bears/new</c> is therefore
    /// <c>bears/new</c>, not <c>bears/{id}</c>. Equally specific templates keep the order of registration.
    /// </summary>
    /// <returns>The entry, or one whose <c>Template</c> is <see langword="null"/> when no template matches.</returns>
    internal static (string Template, Type? ViewModelType) BestMatch(
        IEnumerable<(string Template, Type? ViewModelType)> entries, string uri)
    {
        var best = default((string Template, Type? ViewModelType));
        int[]? bestRank = null;
        foreach (var entry in entries)
        {
            if (!TryMatch(entry.Template, uri))
                continue;

            var rank = Specificity(entry.Template);
            if (bestRank is null || CompareRanks(rank, bestRank) > 0)
            {
                best = entry;
                bestRank = rank;
            }
        }

        return best;
    }

    private static int[] Specificity(string template)
    {
        var (path, _) = Split(template);
        return [.. path.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(segment =>
        {
            var m = Param.Match(segment);
            if (!m.Success) return 3;                                   // literal
            if (m.Groups["opt"].Success) return 0;                      // optional
            var type = m.Groups["type"].Value;
            return type is "" or "?" or "string" ? 1 : 2;               // untyped, or typed
        })];
    }

    private static int CompareRanks(int[] a, int[] b)
    {
        for (var i = 0; i < Math.Min(a.Length, b.Length); i++)
            if (a[i] != b[i])
                return a[i].CompareTo(b[i]);
        return 0;
    }

    /// <summary>
    /// Splits a route string into path and query sections while ignoring '?' characters nested inside curly braces.
    /// </summary>
    private static (string path, string query) Split(string s)
    {
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
    /// Parses a query string into a key-value dictionary.
    /// </summary>
    private static Dictionary<string, string> ParseQuery(string q)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in q.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var p = pair.Split('=', 2);
            result[Uri.UnescapeDataString(p[0])] =
                p.Length > 1 ? Uri.UnescapeDataString(p[1]) : "";   // last value wins on duplicates
        }
        return result;
    }
}
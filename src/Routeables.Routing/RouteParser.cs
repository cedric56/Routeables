using Routeables.Abstraction;
using Routeables.Abstraction.Routing;
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
            if (AmbiguousNames.ContainsKey(simple))
                return;
            if (SerializationTypes.TryGetValue(simple, out var owner) && owner != type)
            {
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

    // {Id:int}  {Tab?}  {Tab::?}  {animate::bool}  {person:Person}  {**path}
    // "::" sets the 'attribute' group, "?" sets 'opt', "**" sets 'rest': the placeholder takes what is left of the path, slashes included
    private static readonly Regex Param =
        new(@"^\{(?<rest>\*\*)?(?<name>\w+)(?::(?<attribute>:)?(?<type>[^}?]*))?(?<opt>\?)?\}$");

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
        var restAt = Array.FindIndex(tSegs, segment => Param.Match(segment) is { Success: true } found && found.Groups["rest"].Success);
        if (restAt >= 0 && restAt != tSegs.Length - 1)
            return $"the catch-all placeholder {tSegs[restAt]} must be the last segment of the template";
        if (OptionalNotLast(tSegs) is { } notLast)
            return $"the optional placeholder {notLast} must be the last segment of the template";
        if (restAt < 0 && uSegs.Length > tSegs.Length)
            return $"the route has {uSegs.Length} segment(s), the template only {tSegs.Length} (unexpected \"{uSegs[tSegs.Length]}\")";

        for (var i = 0; i < tSegs.Length; i++)
        {
            var m = Param.Match(tSegs[i]);

            // {**path}: everything that is left, as it was written (each segment unescaped, the slashes kept). Nothing left is no value.
            if (m.Success && m.Groups["rest"].Success)
            {
                var rest = string.Join('/', uSegs.Skip(i).Select(Uri.UnescapeDataString));
                if (Bind(m, rest, args, viewKeySet, bound) is { } restFailure) return restFailure;
                break;
            }

            var raw = i < uSegs.Length ? Uri.UnescapeDataString(uSegs[i]) : null;

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
        var optional = m.Groups["opt"].Success || m.Groups["rest"].Success;   // a catch-all may be empty

        if (AmbiguousNames.ContainsKey(type))
            return $"the constraint \"{type}\" of {{{name}}} stands for several registered types: write the full name of the type";

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
    /// <c>bears/new</c>, not <c>bears/{id}</c>. When one template is the start of the other, the shorter wins (<c>/products</c> over
    /// <c>/products/{tab?}</c>). Equally specific templates keep the order of registration.
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
            if (m.Groups["rest"].Success) return -1;                    // catch-all
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

        // The same start: the shorter template is the more exact one (/products before /products/{tab?}, /files before /files/{**path}).
        return b.Length.CompareTo(a.Length);
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
                    segments.AddRange(restText.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString));
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

        var route = (path.StartsWith('/') ? "/" : string.Empty) + string.Join('/', segments);
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
            default:
                foreach (var property in values.GetType().GetProperties())
                    if (property.GetIndexParameters().Length == 0 && property.CanRead)
                        bag[property.Name] = property.GetValue(values);
                break;
        }

        return bag;
    }

    private static string Format(object value, string type)
    {
        if (SerializationInfos.TryGetValue(type, out var info))
            return JsonSerializer.Serialize(value, info);
        return value switch
        {
            string text => text,
            bool flag => flag ? "true" : "false",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty,
        };
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
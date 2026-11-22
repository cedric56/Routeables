using Routeables.Abstraction.Routing;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;

namespace Routeables.Routing;

// The types a route can carry: the built-in constraints, and the serializers registered for the others.
public static partial class RouteParser
{
    // Serialization info for complex route parameters (System.Text.Json), by type name, case-insensitive.
    // Private: callers go through RegisterSerializer, so nobody can replace or empty the table.
    // A type is known by its full name (always) and by its simple name, until another type takes it (the simple name is then ambiguous).
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
                // No namespace: the simple name is the full name, stored above. The type owns it; a type that has the same name as a namespace
                // is reached by its full name.
                AmbiguousNames.TryRemove(simple, out _);
                return;
            }

            if (AmbiguousNames.ContainsKey(simple))
                return;
            if (SerializationTypes.TryGetValue(simple, out var owner) && owner != type)
            {
                if (owner.FullName == simple)
                    return; // a type without a namespace keeps its full name

                // A second type with this simple name: nobody owns the name any more.
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

    private static readonly string[] BuiltInConstraints = ["string", "int", "long", "bool", "guid", "decimal", "double"];

    private static bool IsBuiltIn(string type)
        => type is "" or "?" || Array.Exists(BuiltInConstraints, c => string.Equals(c, type, StringComparison.OrdinalIgnoreCase));

    // The registered type of a constraint. A built-in name never refers to a registered type: an application type named Guid, String or Double
    // must not turn {id:guid} into a JSON value (reach it by its full name, {id:Sales.Guid}).
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
                // Catch more than JsonException: an application converter, or a type the info cannot read, may throw anything. An address that
                // cannot be read as the type does not match; it must not throw out of the route search, which runs for every address.
                return false;
            }
            return value is not null;
        }

        var inv = CultureInfo.InvariantCulture;
        switch (type.ToLowerInvariant())
        {
            case "" or "string" or "?": value = raw; return true;
            // The usual spellings are read (+42, 007, True, an upper-case Guid, 1.50, 1e5), and the router writes the address back in the one text
            // Build writes (Canonical), so that every spelling is one page for the history and RouteAddress. A number has no spaces, a Guid has its
            // dashes and no braces.
            case "int": { int v = 0; var ok = int.TryParse(raw, NumberStyles.AllowLeadingSign, inv, out v); value = v; return ok; }
            case "long": { long v = 0; var ok = long.TryParse(raw, NumberStyles.AllowLeadingSign, inv, out v); value = v; return ok; }
            // TryParse ignores spaces and a trailing null character: the text must be the one word, so that "true%00" is not a bool.
            case "bool": { var ok = bool.TryParse(raw, out var v) && raw == raw.Trim() && !raw.Contains('\0'); value = v; return ok; }
            case "guid": { var ok = Guid.TryParseExact(raw, "D", out var v); value = v; return ok; }
            case "decimal": { decimal v = 0; var ok = decimal.TryParse(raw, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, inv, out v); value = v; return ok; }
            case "double": { double v = 0; var ok = double.TryParse(raw, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent, inv, out v) && double.IsFinite(v); value = v; return ok; }
            default:
                value = null;
                return false; // unreachable: Bind rejects an unknown constraint first
        }
    }

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

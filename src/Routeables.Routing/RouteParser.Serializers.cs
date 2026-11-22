using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

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
            // A value has exactly one text. A number is what ToString writes ("42", not "+42", " 42" or "007"); a Guid has its dashes and no braces;
            // a bool has no space around it.
            case "int": { int v = 0; var ok = int.TryParse(raw, NumberStyles.AllowLeadingSign, inv, out v) && v.ToString(inv) == raw; value = v; return ok; }
            case "long": { long v = 0; var ok = long.TryParse(raw, NumberStyles.AllowLeadingSign, inv, out v) && v.ToString(inv) == raw; value = v; return ok; }
            case "bool": { var ok = raw == raw.Trim() && bool.TryParse(raw, out var v); value = ok && bool.Parse(raw); return ok; }
            case "guid": { var ok = Guid.TryParseExact(raw, "D", out var v); value = v; return ok; }
            case "decimal": { decimal v = 0; var ok = !raw.StartsWith('+') && decimal.TryParse(raw, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, inv, out v) && Plain(v).ToString(inv) == raw; value = v; return ok; }
            case "double": { double v = 0; var ok = !raw.StartsWith('+') && double.TryParse(raw, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent, inv, out v) && double.IsFinite(v) && !(v == 0 && raw[0] == '-') && v.ToString(inv) == raw; value = v; return ok; }
            default:
                value = null;
                return false; // unreachable: Bind rejects an unknown constraint first
        }
    }
}

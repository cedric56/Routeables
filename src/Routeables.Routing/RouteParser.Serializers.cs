using System.Collections.Concurrent;
using System.Text.Json.Serialization.Metadata;

namespace Routeables.Routing;

// The serializers registered for the types a route can carry besides the built-in ones.
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
    /// (<c>{c:Sales.Customer}</c>); a nested type by its simple name, or as <c>Outer.Inner</c> or <c>Outer+Inner</c> after its namespace. Registering the same type again replaces its information; when two types share a simple name, the simple
    /// name no longer stands for either of them and only the full names are accepted. Names ignore the case: two types whose full names differ only by it are both refused.
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
                // The names ignore the case: two types whose full names differ only by it cannot be told apart, so the name stands for neither
                // (a template that writes it is refused) instead of silently giving the type that was registered last.
                if (AmbiguousNames.ContainsKey(full))
                    return;
                if (SerializationTypes.TryGetValue(full, out var sameName) && sameName != type)
                {
                    SerializationInfos.TryRemove(full, out _);
                    SerializationTypes.TryRemove(full, out _);
                    AmbiguousNames[full] = true;
                    return;
                }

                SerializationInfos[full] = info;
                SerializationTypes[full] = type;

                // A nested type is Outer+Inner for the runtime and Outer.Inner in the code: both are accepted, unless the dotted name is another type's.
                if (type.IsNested)
                {
                    var written = full.Replace('+', '.');
                    if (!SerializationTypes.TryGetValue(written, out var holder) || holder == type)
                    {
                        SerializationInfos[written] = info;
                        SerializationTypes[written] = type;
                    }
                }
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
                if (type.IsNested && SerializationTypes.TryGetValue(full.Replace('+', '.'), out var written) && written == type)
                {
                    SerializationInfos.TryRemove(full.Replace('+', '.'), out _);
                    SerializationTypes.TryRemove(full.Replace('+', '.'), out _);
                }
            }

            if (SerializationTypes.TryGetValue(type.Name, out var owner) && owner == type)
            {
                SerializationInfos.TryRemove(type.Name, out _);
                SerializationTypes.TryRemove(type.Name, out _);
            }
        }
    }
}

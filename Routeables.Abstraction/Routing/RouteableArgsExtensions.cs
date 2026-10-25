namespace Routeables.Abstraction;

/// <summary>
/// Reads the neutral <see cref="IRouteableArgs.Values"/> bag by name (or by type when the type is unique),
/// instead of picking the first value of a type.
/// </summary>
public static class RouteableArgsExtensions
{
    /// <summary>
    /// Gets the value named <paramref name="name"/>, or the default when there is none (or it is null).
    /// </summary>
    /// <exception cref="InvalidOperationException">Two values have that name.</exception>
    /// <exception cref="InvalidCastException">The value is not a <typeparamref name="T"/>.</exception>
    public static T? Get<T>(this IRouteableArgs args, string name)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var value = Named(args, name);
        return value switch
        {
            null => default,
            T t => t,
            _ => throw new InvalidCastException($"The value \"{name}\" is a {value.GetType().Name}, not a {typeof(T).Name}.")
        };
    }

    // The value named name, or null when there is none.
    private static object? Named(IRouteableArgs args, string name)
    {
        var matches = args.Values.Where(e => e.name == name).ToList();
        if (matches.Count > 1)
            throw new InvalidOperationException($"{matches.Count} values are named \"{name}\"; a name must be used once.");

        return matches.Count == 0 ? null : matches[0].value;
    }

    /// <summary>
    /// Gets the value named <paramref name="name"/>; <see langword="false"/> when there is none or it is not a <typeparamref name="T"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">Two values have that name.</exception>
    public static bool TryGet<T>(this IRouteableArgs args, string name, out T value)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (Named(args, name) is T t)
        {
            value = t;
            return true;
        }

        value = default!;
        return false;
    }

    /// <summary>
    /// Gets the only value of type <typeparamref name="T"/>, or the default when there is none.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Several values are a <typeparamref name="T"/> (two <see cref="Action"/>s, say): name them and use <see cref="Get{T}(IRouteableArgs, string)"/>.
    /// </exception>
    public static T? Get<T>(this IRouteableArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var matches = args.Values.Select(e => e.value).OfType<T>().ToList();
        return matches.Count switch
        {
            0 => default,
            1 => matches[0],
            _ => throw new InvalidOperationException(
                $"{matches.Count} values are a {typeof(T).Name}; give them names and read them with Get<{typeof(T).Name}>(name).")
        };
    }

    // ------------------------------------------------------------------ typed keys

    /// <summary>Adds a neutral value under a typed key: the type of the value is checked when it is written.</summary>
    public static IRouteableArgs WithValue<T>(this IRouteableArgs args, RouteableKey<T> key, T value)
    {
        ArgumentNullException.ThrowIfNull(args);
        return args.WithValue(key.Name, value);
    }

    /// <summary>Adds a view model value under a typed key.</summary>
    public static IRouteableArgs WithViewModel<T>(this IRouteableArgs args, RouteableKey<T> key, T value, bool attribute = false)
    {
        ArgumentNullException.ThrowIfNull(args);
        return args.WithViewModel(key.Name, value, attribute);
    }

    /// <summary>Adds a view value under a typed key.</summary>
    public static IRouteableArgs WithView<T>(this IRouteableArgs args, RouteableKey<T> key, T value, bool attribute = false)
    {
        ArgumentNullException.ThrowIfNull(args);
        return args.WithView(key.Name, value, attribute);
    }

    /// <summary>
    /// Gets the value under <paramref name="key"/>, or the default when there is none; the type comes from the key.
    /// </summary>
    /// <exception cref="InvalidOperationException">Two values have that name.</exception>
    /// <exception cref="InvalidCastException">The value is not a <typeparamref name="T"/>.</exception>
    public static T? Get<T>(this IRouteableArgs args, RouteableKey<T> key) => args.Get<T>(key.Name);

    /// <summary>Gets the value under <paramref name="key"/>; <see langword="false"/> when there is none.</summary>
    /// <exception cref="InvalidOperationException">Two values have that name.</exception>
    public static bool TryGet<T>(this IRouteableArgs args, RouteableKey<T> key, out T value) => args.TryGet(key.Name, out value);
}

/// <summary>
/// The name of a value in <see cref="IRouteableArgs"/>, with its type: <c>static readonly RouteableKey&lt;Animal&gt; Animal = new("animal");</c>.
/// Declared once, it is used to write and to read the value, so the name is not repeated as a string and the type cannot differ.
/// </summary>
/// <typeparam name="T">The type of the value.</typeparam>
/// <param name="Name">The name the value is stored under.</param>
public readonly record struct RouteableKey<T>(string Name);

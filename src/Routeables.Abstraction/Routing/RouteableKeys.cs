namespace Routeables.Abstraction;

/// <summary>
/// Represents a composite key used to uniquely identify routeable views and view models within the container 
/// based on a route identifier and an optional associated type.
/// </summary>
/// <param name="key">The base key object or string identifier.</param>
/// <param name="Type">The optional type associated with the key.</param>
internal readonly record struct RouteableKeys(object key, Type? Type)
{
    /// <summary>
    /// Creates a new instance of <see cref="RouteableKeys"/> with the specified base key and optional type.
    /// </summary>
    /// <param name="key">The base key object.</param>
    /// <param name="type">The optional associated type.</param>
    /// <returns>A new <see cref="RouteableKeys"/> instance.</returns>
    public static RouteableKeys For(object key, Type? type = null)
        => new(key, type);

    /// <summary>
    /// Returns a string representation of the composite key, including the type name if present.
    /// </summary>
    /// <returns>A formatted string representation of the key.</returns>
    public override string ToString() => Type switch
    {
        not null => $"{key}:{Type.FullName ?? Type.Name}",
        null => $"{key}"
    };

    /// <summary>
    /// Computes the registration key pair (view key and view model key) for a given view model type and optional route.
    /// </summary>
    /// <param name="type">The view model type.</param>
    /// <param name="route">Optional explicit route override.</param>
    /// <returns>A tuple containing the resolved view key and view model key.</returns>
    public static (string viewKey, string viewModelKey) GetKeys(Type type, string? route = null)
    {
        return (GetRoute(type, route), For($"vm:{type.Name}", type).ToString());
    }

    public static string GetRoute(Type type, string? route = null)
    {
        return (
            !string.IsNullOrWhiteSpace(route) ? $"{For(route)}" : For($"v:{type.Name}", type).ToString()
        );
    }

    /// <summary>The template of a view: its route (a view registered without a template is matched by its route).</summary>
    public static string GetTemplate(Type type, string? route = null) => GetRoute(type, route);
}
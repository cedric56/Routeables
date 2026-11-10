namespace Routeables.Abstraction.Routing;

/// <summary>
/// Represents a generic entry container holding an optional value, name identifier, and an attribute flag.
/// </summary>
/// <param name="value">The underlying object value.</param>
/// <param name="name">The name identifier associated with the entry.</param>
/// <param name="attribute">A value indicating whether this entry represents an attribute.</param>
public record RouteableArgument(object? value = null, string? name = null, bool attribute = false);

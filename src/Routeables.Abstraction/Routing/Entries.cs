namespace Routeables.Abstraction;

/// <summary>
/// Represents a generic entry container holding an optional value, name identifier, and an attribute flag.
/// </summary>
/// <param name="value">The underlying object value.</param>
/// <param name="name">The name identifier associated with the entry.</param>
/// <param name="attribute">A value indicating whether this entry represents an attribute.</param>
public record RouteableArgument(object? value = null, string? name = null, bool attribute = false);

/// <summary>
/// Represents a property entry configuration containing a target type and an optional function to resolve its name from an attribute.
/// </summary>
/// <param name="Type">The target <see cref="System.Type"/> of the property or attribute.</param>
/// <param name="getName">An optional function to extract or generate a name string from the attribute instance.</param>
/// <param name="FromContainer">
/// Whether a property with this attribute that received no value is resolved from the container by its type
/// (<c>[Inject]</c>), which is the default. A value attribute (<c>[Parameter]</c>, <c>[QueryProperty]</c>) passes
/// <see langword="false"/>: it only receives what the route change gave, so a missing value stays missing instead of
/// ending in a misleading "no service for type String".
/// </param>
internal record RouteablePropertyEntry(Type Type, Func<Attribute, string>? getName = null, bool FromContainer = true);
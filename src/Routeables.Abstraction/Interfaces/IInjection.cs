namespace Routeables.Abstraction;


/// <summary>
/// Marker attributes used by <c>InjectProperties</c> when assigning
/// DI services and routing/view parameters on a created view.
/// </summary>
internal interface IInjection
{
    /// <summary>Attribute types that identify injectable properties (e.g. [Inject], [Parameter]).</summary>
    IReadOnlyList<RouteablePropertyEntry> AttributeTypes { get; }
}

internal sealed class PropertyInjection : IInjection
{
    public IReadOnlyList<RouteablePropertyEntry> AttributeTypes { get; }

    public PropertyInjection(params RouteablePropertyEntry[] attributeTypes)
    {
        ArgumentNullException.ThrowIfNull(attributeTypes);
        if (attributeTypes.Length == 0)
            throw new ArgumentException("At least one attribute type is required.", nameof(attributeTypes));

        foreach (var t in attributeTypes)
        {
            if (t.Type is null)
                throw new ArgumentException("Attribute types cannot contain null.", nameof(attributeTypes));
            if (!typeof(Attribute).IsAssignableFrom(t.Type))
                throw new ArgumentException($"{t.Type.FullName} is not an Attribute type.", nameof(attributeTypes));
        }

        AttributeTypes = Array.AsReadOnly(attributeTypes);
    }
}

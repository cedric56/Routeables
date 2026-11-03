using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Routeables.Abstraction;

/// <summary>
/// What the injection needs to know about a type to fill it (see <see cref="IInjection"/>): its attributes, and its properties with
/// the attributes they carry and a way to read and write them. The source generator writes it for the types it registers, which then
/// are filled without any reflection; a type registered with its own generic type is read from the members the trimmer keeps.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]    // for the code the source generator writes
public sealed class InjectableType
{
    private static readonly ConditionalWeakTable<Type, InjectableType> Reflected = new();

    /// <param name="typeAttributes">The attributes of the type, those it inherits included.</param>
    /// <param name="properties">The instance properties that have a setter.</param>
    public InjectableType(IReadOnlyList<Attribute> typeAttributes, IReadOnlyList<InjectableProperty> properties)
    {
        ArgumentNullException.ThrowIfNull(typeAttributes);
        ArgumentNullException.ThrowIfNull(properties);
        TypeAttributes = typeAttributes;
        Properties = properties;
    }

    /// <summary>The attributes of the type, those it inherits included.</summary>
    public IReadOnlyList<Attribute> TypeAttributes { get; }

    /// <summary>The instance properties that have a setter.</summary>
    public IReadOnlyList<InjectableProperty> Properties { get; }

    /// <summary>The names the type-level attributes give to properties (<c>[QueryProperty("Name", "name")]</c>), whatever the registered attributes make of them.</summary>
    internal HashSet<string> NamesGivenBy(IInjection injection)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var attribute in TypeAttributes)
            foreach (var entry in injection.AttributeTypes)
                if (entry.getName is not null && entry.Type.IsInstanceOfType(attribute) && entry.getName(attribute) is { Length: > 0 } name)
                    names.Add(name);
        return names;
    }

    /// <summary>
    /// The type read by reflection, once: used for the types that registered themselves with their static type. The members it reads
    /// are the ones <typeparamref name="T"/> is annotated to keep, so a trimmed or ahead-of-time compiled application still has them.
    /// </summary>
    internal static InjectableType Reflect<[DynamicallyAccessedMembers(InjectableMembers.Properties)] T>()
    {
        var type = typeof(T);
        if (Reflected.TryGetValue(type, out var known))
            return known;

        var attributes = type.GetCustomAttributes(inherit: true).OfType<Attribute>().ToList();
        var properties = new List<InjectableProperty>();
        foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (!property.CanWrite || property.GetIndexParameters().Length > 0)
                continue;

            var captured = property;
            properties.Add(new InjectableProperty(
                property.Name,
                property.PropertyType,
                attributeType => captured.IsDefined(attributeType, inherit: true),
                (instance, value) => captured.SetValue(instance, value),
                property.CanRead ? instance => captured.GetValue(instance) : null));
        }

        return Reflected.GetValue(type, _ => new InjectableType(attributes, properties));
    }
}

/// <summary>What the library asks the trimmer and the ahead-of-time compiler to keep of the views and view models it builds.</summary>
internal static class InjectableMembers
{
    /// <summary>The properties the injection reads and writes (see <see cref="InjectableType.Reflect{T}"/>).</summary>
    internal const DynamicallyAccessedMemberTypes Properties =
        DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties;

    /// <summary>What a type that is created by dependency injection and then injected needs: its constructors and its properties.</summary>
    internal const DynamicallyAccessedMemberTypes Created = DynamicallyAccessedMemberTypes.PublicConstructors | Properties;
}

/// <summary>A property of an <see cref="InjectableType"/>.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]    // for the code the source generator writes
public sealed class InjectableProperty
{
    private readonly Func<Type, bool> hasAttribute;

    /// <param name="name">The name of the property.</param>
    /// <param name="propertyType">Its type.</param>
    /// <param name="attributes">The full names (<see cref="Type.FullName"/>) of the attributes it carries and of their base types.</param>
    /// <param name="set">Writes it.</param>
    /// <param name="get">Reads it; <see langword="null"/> when it has no getter.</param>
    public InjectableProperty(
        string name, Type propertyType, IReadOnlyCollection<string> attributes, Action<object, object?> set, Func<object, object?>? get)
        : this(name, propertyType, type => type.FullName is { } full && attributes.Contains(full), set, get)
    {
        ArgumentNullException.ThrowIfNull(attributes);
    }

    internal InjectableProperty(
        string name, Type propertyType, Func<Type, bool> hasAttribute, Action<object, object?> set, Func<object, object?>? get)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(propertyType);
        ArgumentNullException.ThrowIfNull(set);
        Name = name;
        PropertyType = propertyType;
        this.hasAttribute = hasAttribute;
        Set = set;
        Get = get;
    }

    /// <summary>The name of the property.</summary>
    public string Name { get; }

    /// <summary>The type of the property.</summary>
    internal Type PropertyType { get; }

    /// <summary>Writes the property.</summary>
    public Action<object, object?> Set { get; }

    /// <summary>Reads the property; <see langword="null"/> when it has no getter.</summary>
    public Func<object, object?>? Get { get; }

    /// <summary>Whether the property carries an attribute of <paramref name="attributeType"/> (or of a type that derives from it).</summary>
    internal bool Has(Type attributeType) => hasAttribute(attributeType);
}

/// <summary>
/// The <see cref="InjectableType"/> of the types the source generator registered, found by the exact type of the instance. A type
/// that is not in it is read by reflection, when its static type is its exact type (the registrations of a view or a view model
/// are generic and annotated, so the members it needs are kept). It is meant for the generated code, not for the application.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
internal sealed class InjectionTable
{
    private readonly Dictionary<Type, InjectableType> types = [];

    /// <summary>Describes <paramref name="type"/>.</summary>
    public void Add(Type type, InjectableType injectable)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(injectable);
        types[type] = injectable;
    }

    internal InjectableType? Find(Type type) => types.GetValueOrDefault(type);

    /// <summary>
    /// How <paramref name="instance"/> is filled: as the generator described its type, else as reflection reads <typeparamref name="T"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The instance is of a type derived from <typeparamref name="T"/> that nobody described:
    /// nothing says which of its members are kept, so it is refused rather than half filled.</exception>
    internal static InjectableType Resolve<[DynamicallyAccessedMembers(InjectableMembers.Properties)] T>(IServiceProvider sp, T instance)
        where T : notnull
    {
        var type = instance.GetType();
        if (sp.GetService<InjectionTable>()?.Find(type) is { } described)
            return described;

        return type == typeof(T)
            ? InjectableType.Reflect<T>()
            : throw new InvalidOperationException(
                $"{type} is not described: register it with its own type ({typeof(T)} is its static type), or let the source generator describe it.");
    }
}

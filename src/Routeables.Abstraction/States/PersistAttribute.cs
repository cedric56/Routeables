namespace Routeables.Abstraction.States;

/// <summary>
/// Marks a property of a view model whose value comes back after the system killed the process of the application (or at the next start): the
/// source generator writes the code that saves and reads it, without reflection. The value is kept next to the address of the page, with the
/// pages of the history (see <c>RouteableState</c> in <c>Routeables.Services</c>).
/// </summary>
/// <remarks>
/// <para>
/// The property must be an instance property with a getter and a setter that the code of the same assembly can call (<see langword="public"/> or
/// <see langword="internal"/>, not <see langword="init"/>); the class must not be generic. Nothing is generated otherwise and the generator says why (<c>ROUT401</c>).
/// </para>
/// <para>
/// What is kept: text, <c>bool</c>, numbers, <c>char</c>, <c>Guid</c>, enums, dates (<c>DateTime</c>, <c>DateTimeOffset</c>, <c>TimeSpan</c>, <c>DateOnly</c>,
/// <c>TimeOnly</c>) and the <c>Nullable</c> of these. Any other type (a record of the draft of a form, a list) needs a format, and you choose it: a
/// <see cref="Converter"/> on the property, or a converter for the type (<see cref="RouteablePersistence.RegisterConverter{T}(IPersistConverter{T})"/>;
/// <see cref="RouteablePersistence.RegisterJson{T}"/> is one ready-made, trim-safe choice). A type with no format is left out (the rest is kept). Keep a form as <b>one</b> small record of plain values, and an entity as its identifier: the snapshot holds what the user typed,
/// not the objects of the application.
/// </para>
/// <para>Services, commands, collections that are bound to a control and anything that cannot be read back do not belong here.</para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class PersistAttribute : Attribute
{
    /// <summary>
    /// The class that writes and reads this property (it implements <see cref="IPersistConverter{T}"/> for the type of the property and has a public
    /// constructor with no parameter). Without it, the converter registered for the type is used, then the built-in formats. The generator checks it
    /// (<c>ROUT401</c>).
    /// </summary>
    public Type? Converter { get; set; }
}

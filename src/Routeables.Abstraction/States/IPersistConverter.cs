namespace Routeables.Abstraction.States;

/// <summary>
/// How a value of type <typeparamref name="T"/> is written as text and read back by <see cref="PersistAttribute"/>: the format is yours (JSON, CSV,
/// an identifier, a base64 blob, a custom string). Give it to one property with <see cref="PersistAttribute.Converter"/>, or to every property of the
/// type with <see cref="RouteablePersistence.RegisterConverter{T}(IPersistConverter{T})"/>.
/// </summary>
/// <typeparam name="T">The type of the property.</typeparam>
public interface IPersistConverter<T>
{
    /// <summary>The text that stands for <paramref name="value"/>; <see langword="null"/> leaves the value out. Never called for <see langword="null"/>.</summary>
    string? Write(T value);

    /// <summary>Reads the text back; <see langword="false"/> leaves the property as the view model built it.</summary>
    bool TryRead(string text, out T value);
}

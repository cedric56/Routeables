using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Routeables.Abstraction.States;

/// <summary>
/// What the code generated for <see cref="PersistAttribute"/> registers: how to save and read back the properties of a view model. The
/// application does not call it, except <see cref="RegisterConverter{T}(IPersistConverter{T})"/> (or <see cref="RegisterJson{T}"/>) for the values that are not scalars.
/// </summary>
public static class RouteablePersistence
{
    /// <summary>The start of the key of every value this class writes into the dictionary of the saved values.</summary>
    public const string KeyPrefix = "persist:";

    /// <summary>The longest text kept for one property (a longer one is left out): the saved values go to places that are small (an Android bundle).</summary>
    public static int MaxValueLength { get; set; } = 64 * 1024;

    private sealed record Entry(Action<object, PersistBag> Save, Action<object, PersistBag> Load);

    private static readonly ConcurrentDictionary<Type, Entry> Entries = new();

    /// <summary>Registers the code that saves and reads back the properties of <typeparamref name="TViewModel"/> (the generated code calls it at start).</summary>
    public static void Register<TViewModel>(Action<TViewModel, PersistBag> save, Action<TViewModel, PersistBag> load) where TViewModel : class
    {
        ArgumentNullException.ThrowIfNull(save);
        ArgumentNullException.ThrowIfNull(load);
        Entries[typeof(TViewModel)] = new Entry((vm, bag) => save((TViewModel)vm, bag), (vm, bag) => load((TViewModel)vm, bag));
    }

    /// <summary>
    /// Says how every <c>[Persist]</c> property of type <typeparamref name="T"/> is written and read, in the format of your choice. It also replaces a
    /// built-in format (<c>DateTime</c> as a ticks count, say). A converter given to one property (<see cref="PersistAttribute.Converter"/>) wins over it.
    /// </summary>
    public static void RegisterConverter<T>(IPersistConverter<T> converter)
    {
        ArgumentNullException.ThrowIfNull(converter);
        RegisterConverter<T>(converter.Write, converter.TryRead);
    }

    /// <summary>Delegate form of <see cref="RegisterConverter{T}(IPersistConverter{T})"/>.</summary>
    public static void RegisterConverter<T>(Func<T, string?> write, TryRead<T> read)
    {
        ArgumentNullException.ThrowIfNull(write);
        ArgumentNullException.ThrowIfNull(read);
        PersistCodec.Converters[typeof(T)] = new PersistCodec.Adapter(
            value => write((T)value),
            text => read(text, out var value) ? value : null);
    }

    /// <summary>Reads a value from its text (the shape of <c>int.TryParse</c>).</summary>
    public delegate bool TryRead<T>(string text, out T value);

    /// <summary>
    /// One ready-made format for a type that is not a scalar: JSON, with the information System.Text.Json's own source generator made for it (no
    /// reflection, safe to trim). Any other format goes through <see cref="RegisterConverter{T}(IPersistConverter{T})"/>.
    /// </summary>
    public static void RegisterJson<T>(JsonTypeInfo<T> info)
    {
        ArgumentNullException.ThrowIfNull(info);
        RegisterConverter<T>(
            value => JsonSerializer.Serialize(value, info),
            (string text, out T value) =>
            {
                var read = JsonSerializer.Deserialize(text, info);
                value = read!;
                return read is not null;
            });
    }

    /// <summary>Whether a view model of this type has a property that is saved.</summary>
    public static bool Has(Type viewModelType)
    {
        for (var type = viewModelType; type is not null; type = type.BaseType)
        {
            if (Entries.ContainsKey(type))
                return true;
        }

        return false;
    }

    /// <summary>Writes the <c>[Persist]</c> properties of <paramref name="viewModel"/> (those of its base classes too) into <paramref name="into"/>.</summary>
    /// <returns><see langword="true"/> when the view model has some.</returns>
    public static bool Save(object viewModel, IDictionary<string, string> into)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(into);
        var found = false;
        var bag = new PersistBag(into);
        for (var type = viewModel.GetType(); type is not null; type = type.BaseType)
        {
            if (Entries.TryGetValue(type, out var entry))
            {
                found = true;
                entry.Save(viewModel, bag);
            }
        }

        return found;
    }

    /// <summary>Gives back to <paramref name="viewModel"/> the values <see cref="Save"/> wrote; a value that cannot be read leaves its property as it is.</summary>
    /// <returns><see langword="true"/> when the view model has <c>[Persist]</c> properties.</returns>
    public static bool Load(object viewModel, IReadOnlyDictionary<string, string> from)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(from);
        var found = false;
        var bag = new PersistBag(from);
        for (var type = viewModel.GetType(); type is not null; type = type.BaseType)
        {
            if (Entries.TryGetValue(type, out var entry))
            {
                found = true;
                entry.Load(viewModel, bag);
            }
        }

        return found;
    }
}

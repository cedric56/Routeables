namespace Routeables.Abstraction.States;

/// <summary>
/// The values of a view model as they are saved and read back (what the code generated for <see cref="PersistAttribute"/> uses). One
/// value is one entry of a text dictionary; the value of a property that cannot be written or read is left out and does not stop the others.
/// </summary>
public sealed class PersistBag
{
    private readonly IDictionary<string, string>? _write;
    private readonly IReadOnlyDictionary<string, string>? _read;
    // Set for one Save or one Load, which go from the most derived class to its bases. The classes already done are recorded, so a hidden
    // base-class property (same name, "new") neither overwrites nor reads the value. Within one class a name may be asked for repeatedly.
    private readonly HashSet<string>? _taken;
    private readonly HashSet<string>? _current;

    internal PersistBag(IDictionary<string, string> write, bool exclusive = false)
    {
        _write = write;
        if (exclusive) { _taken = []; _current = []; }
    }

    internal PersistBag(IReadOnlyDictionary<string, string> read, bool exclusive = false)
    {
        _read = read;
        if (exclusive) { _taken = []; _current = []; }
    }

    /// <summary>The class that was done is over: its names are taken for the next one (its base class).</summary>
    internal void NextClass()
    {
        _taken?.UnionWith(_current!);
        _current?.Clear();
    }

    private bool Taken(string name)
    {
        if (_taken is null)
            return false;
        if (_taken.Contains(name))
            return true;
        _current!.Add(name);
        return false;
    }

    /// <summary>Writes the value of a property; nothing is written when it cannot be (a type that is not supported, a text that is too long).</summary>
    /// <returns><see langword="true"/> when the value was written.</returns>
    public bool Set<T>(string name, T value) => Set(name, value, null);

    /// <summary>Like <see cref="Set{T}(string, T)"/>, with the converter the property asked for.</summary>
    public bool Set<T>(string name, T value, IPersistConverter<T>? converter)
    {
        if (_write is null)
            return false;
        try
        {
            if (Taken(name))
                return true;

            if (value is null)
            {
                _write[RouteablePersistence.KeyPrefix + name] = "n";
                return true;
            }

            if (PersistCodec.TryWrite(value, converter, out var text) && text.Length <= RouteablePersistence.MaxValueLength)
            {
                _write[RouteablePersistence.KeyPrefix + name] = "v" + text;
                return true;
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Best effort: leave the value out.
        }

        // Write nothing: what an earlier save left under this name is no longer the property's value and must not come back.
        try { _write.Remove(RouteablePersistence.KeyPrefix + name); }
        catch (Exception ex) when (ex is not OutOfMemoryException) { }
        return false;
    }

    /// <summary>Reads the value of a property; <see langword="false"/> when it was not saved or cannot be read (the property keeps its value).</summary>
    public bool TryGet<T>(string name, out T value) => TryGet(name, null, out value);

    /// <summary>Like <see cref="TryGet{T}(string, out T)"/>, with the converter the property asked for.</summary>
    public bool TryGet<T>(string name, IPersistConverter<T>? converter, out T value)
    {
        value = default!;
        if (Taken(name))
            return false;
        if (_read is null || !_read.TryGetValue(RouteablePersistence.KeyPrefix + name, out var stored) || stored.Length == 0)
            return false;

        try
        {
            if (stored[0] == 'n')
                return default(T) is null; // null is a value only for a type that can hold it

            if (stored[0] == 'v' && PersistCodec.TryRead<T>(stored.AsSpan(1).ToString(), converter, out var read))
            {
                value = read;
                return true;
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Best effort: the property keeps its value.
        }

        return false;
    }

    /// <summary>Writes <paramref name="get"/> of the view model (a getter that throws leaves the value out).</summary>
    public void Capture<TViewModel, T>(TViewModel viewModel, string name, Func<TViewModel, T> get, IPersistConverter<T>? converter = null)
    {
        try { Set(name, get(viewModel), converter); }
        catch (Exception ex) when (ex is not OutOfMemoryException) { }
    }

    /// <summary>Reads a value into the view model with <paramref name="set"/> (a setter that throws leaves the property as it is).</summary>
    public void Restore<TViewModel, T>(TViewModel viewModel, string name, Action<TViewModel, T> set, IPersistConverter<T>? converter = null)
    {
        try
        {
            if (TryGet<T>(name, converter, out var value))
                set(viewModel, value);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { }
    }
}

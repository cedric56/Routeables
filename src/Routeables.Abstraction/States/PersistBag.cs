using System.Globalization;

namespace Routeables.Abstraction.States;

/// <summary>
/// The values of a view model as they are saved and read back (what the code generated for <see cref="PersistAttribute"/> uses). One
/// value is one entry of a text dictionary; the value of a property that cannot be written or read is left out and does not stop the others.
/// </summary>
public sealed class PersistBag
{
    private readonly IDictionary<string, string>? _write;
    private readonly IReadOnlyDictionary<string, string>? _read;

    internal PersistBag(IDictionary<string, string> write) => _write = write;

    internal PersistBag(IReadOnlyDictionary<string, string> read) => _read = read;

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
            // best effort: the value is left out
        }

        return false;
    }

    /// <summary>Reads the value of a property; <see langword="false"/> when it was not saved or cannot be read (the property keeps its value).</summary>
    public bool TryGet<T>(string name, out T value) => TryGet(name, null, out value);

    /// <summary>Like <see cref="TryGet{T}(string, out T)"/>, with the converter the property asked for.</summary>
    public bool TryGet<T>(string name, IPersistConverter<T>? converter, out T value)
    {
        value = default!;
        if (_read is null || !_read.TryGetValue(RouteablePersistence.KeyPrefix + name, out var stored) || stored.Length == 0)
            return false;

        try
        {
            if (stored[0] == 'n')
                return default(T) is null;   // null is a value only for a type that can hold it

            if (stored[0] == 'v' && PersistCodec.TryRead<T>(stored.AsSpan(1).ToString(), converter, out var read))
            {
                value = read;
                return true;
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // best effort: the property keeps its value
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

internal static class PersistCodec
{
    internal sealed record Adapter(Func<object, string?> Write, Func<string, object?> Read);

    // The formats registered for a type (RouteablePersistence.RegisterConverter, RegisterJson).
    internal static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, Adapter> Converters = new();

    internal static bool TryWrite<T>(T value, IPersistConverter<T>? converter, out string text)
    {
        var inv = CultureInfo.InvariantCulture;
        if (converter is not null)
        {
            var own = converter.Write(value);
            text = own ?? "";
            return own is not null;
        }

        if (!Converters.IsEmpty && (Converters.TryGetValue(value!.GetType(), out var adapter) || Converters.TryGetValue(typeof(T), out adapter)))
        {
            var written = adapter.Write(value!);
            text = written ?? "";
            return written is not null;
        }

        switch (value)
        {
            case string s: text = s; return true;
            case bool b: text = b ? "true" : "false"; return true;
            case char c: text = c.ToString(); return true;
            case Guid g: text = g.ToString("D", inv); return true;
            case DateTime d: text = d.ToString("O", inv); return true;
            case DateTimeOffset o: text = o.ToString("O", inv); return true;
            case TimeSpan t: text = t.ToString("c", inv); return true;
            case DateOnly d: text = d.ToString("O", inv); return true;
            case TimeOnly t: text = t.ToString("O", inv); return true;
            case Enum e: text = e.ToString(); return true;
            case IFormattable f when value.GetType().IsPrimitive || value is decimal || value is Int128 || value is UInt128:
                text = f.ToString(value is double or float ? "R" : null, inv);
                return true;
        }

        text = "";
        return false;
    }

    // A named member, or (for [Flags]) a combination of named members; a bare number that names nothing is refused.
    private static bool IsKnown(Type type, object value)
    {
        if (Enum.IsDefined(type, value))
            return true;

        if (!type.IsDefined(typeof(FlagsAttribute), inherit: false))
            return false;

        var name = value.ToString();
        return !string.IsNullOrEmpty(name) && !char.IsDigit(name[0]) && name[0] != '-';
    }

    internal static bool TryRead<T>(string text, IPersistConverter<T>? converter, out T value)
    {
        value = default!;
        var type = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        var inv = CultureInfo.InvariantCulture;
        object? read = null;

        if (converter is not null)
            return converter.TryRead(text, out value);

        if (!Converters.IsEmpty && Converters.TryGetValue(type, out var adapter))
            read = adapter.Read(text);
        else if (type == typeof(string)) read = text;
        else if (type == typeof(bool)) read = bool.TryParse(text, out var b) ? b : null;
        else if (type == typeof(char)) read = text.Length == 1 ? text[0] : null;
        else if (type == typeof(int)) read = int.TryParse(text, NumberStyles.Integer, inv, out var i) ? i : null;
        else if (type == typeof(long)) read = long.TryParse(text, NumberStyles.Integer, inv, out var l) ? l : null;
        else if (type == typeof(short)) read = short.TryParse(text, NumberStyles.Integer, inv, out var sh) ? sh : null;
        else if (type == typeof(byte)) read = byte.TryParse(text, NumberStyles.Integer, inv, out var by) ? by : null;
        else if (type == typeof(sbyte)) read = sbyte.TryParse(text, NumberStyles.Integer, inv, out var sb) ? sb : null;
        else if (type == typeof(uint)) read = uint.TryParse(text, NumberStyles.Integer, inv, out var ui) ? ui : null;
        else if (type == typeof(ulong)) read = ulong.TryParse(text, NumberStyles.Integer, inv, out var ul) ? ul : null;
        else if (type == typeof(ushort)) read = ushort.TryParse(text, NumberStyles.Integer, inv, out var us) ? us : null;
        else if (type == typeof(double)) read = double.TryParse(text, NumberStyles.Float, inv, out var d) ? d : null;
        else if (type == typeof(float)) read = float.TryParse(text, NumberStyles.Float, inv, out var f) ? f : null;
        else if (type == typeof(decimal)) read = decimal.TryParse(text, NumberStyles.Number, inv, out var m) ? m : null;
        else if (type == typeof(Guid)) read = Guid.TryParse(text, out var g) ? g : null;
        else if (type == typeof(DateTime)) read = DateTime.TryParse(text, inv, DateTimeStyles.RoundtripKind, out var dt) ? dt : null;
        else if (type == typeof(DateTimeOffset)) read = DateTimeOffset.TryParse(text, inv, DateTimeStyles.RoundtripKind, out var dto) ? dto : null;
        else if (type == typeof(TimeSpan)) read = TimeSpan.TryParse(text, inv, out var ts) ? ts : null;
        else if (type == typeof(DateOnly)) read = DateOnly.TryParse(text, inv, DateTimeStyles.None, out var d1) ? d1 : null;
        else if (type == typeof(TimeOnly)) read = TimeOnly.TryParse(text, inv, DateTimeStyles.None, out var t1) ? t1 : null;
        else if (type.IsEnum) read = Enum.TryParse(type, text, ignoreCase: false, out var e) && IsKnown(type, e!) ? e : null;

        if (read is null)
            return false;

        value = (T)read;
        return true;
    }
}

using System.Globalization;

namespace Routeables.Abstraction.States;

/// <summary>How a value is written as text and read back: a converter the property asked for, a format registered for its type, or the built-in types.</summary>
internal static class PersistCodec
{
    internal sealed record Adapter(Func<object, string?> Write, Func<string, object?> Read);

    // The formats registered for a type (RouteablePersistence.RegisterConverter, RegisterJson).
    internal static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, Adapter> Converters = new();

    internal static bool TryWrite<T>(T value, IPersistConverter<T>? converter, out string text)
    {
        if (converter is not null)
        {
            var own = converter.Write(value);
            text = own ?? "";
            return own is not null;
        }

        // The declared property type decides, as for reading (a converter of Dog on a property of Animal is not found when reading, so it is not
        // used when writing). A converter of DateTime? and one of DateTime both serve a DateTime? property.
        if (!Converters.IsEmpty && (Converters.TryGetValue(typeof(T), out var adapter) || (Nullable.GetUnderlyingType(typeof(T)) is { } inner && Converters.TryGetValue(inner, out adapter))))
        {
            var written = adapter.Write(value!);
            text = written ?? "";
            return written is not null;
        }

        // Reading gives back the declared property type: a value of another type (an int in a property of type object) would be written and never read.
        if (value!.GetType() != (Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T)))
        {
            text = "";
            return false;
        }

        return TryWriteBuiltIn(value, out text);
    }

    // The types that need no converter: text, booleans, numbers, dates, enums.
    private static bool TryWriteBuiltIn(object value, out string text)
    {
        var inv = CultureInfo.InvariantCulture;
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
            case Enum e:
                // A number that names no member reads back as nothing (see the read side), so it is not written.
                if (!IsKnown(e.GetType(), e)) { text = ""; return false; }
                text = e.ToString();
                return true;
            case IFormattable f when value.GetType().IsPrimitive || value is decimal || value is Int128 || value is UInt128:
                text = f.ToString(value is double or float ? "R" : null, inv);
                return true;
        }

        text = "";
        return false;
    }

    // A named member or, for [Flags], a combination of named members; a bare number that names nothing is rejected.
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
        if (converter is not null)
            return converter.TryRead(text, out value);

        var type = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        var read = !Converters.IsEmpty && (Converters.TryGetValue(typeof(T), out var adapter) || Converters.TryGetValue(type, out adapter))
            ? adapter.Read(text)
            : ReadBuiltIn(type, text);
        if (read is null)
            return false;

        value = (T)read;
        return true;
    }

    // The value a text stands for in a built-in type, or null when the text is not one (or the type is not built in).
    private static object? ReadBuiltIn(Type type, string text)
    {
        var inv = CultureInfo.InvariantCulture;
        if (type == typeof(string)) return text;
        if (type == typeof(bool)) return bool.TryParse(text, out var b) ? b : null;
        if (type == typeof(char)) return text.Length == 1 ? text[0] : null;
        if (type == typeof(int)) return int.TryParse(text, NumberStyles.Integer, inv, out var i) ? i : null;
        if (type == typeof(long)) return long.TryParse(text, NumberStyles.Integer, inv, out var l) ? l : null;
        if (type == typeof(short)) return short.TryParse(text, NumberStyles.Integer, inv, out var sh) ? sh : null;
        if (type == typeof(byte)) return byte.TryParse(text, NumberStyles.Integer, inv, out var by) ? by : null;
        if (type == typeof(sbyte)) return sbyte.TryParse(text, NumberStyles.Integer, inv, out var sb) ? sb : null;
        if (type == typeof(uint)) return uint.TryParse(text, NumberStyles.Integer, inv, out var ui) ? ui : null;
        if (type == typeof(ulong)) return ulong.TryParse(text, NumberStyles.Integer, inv, out var ul) ? ul : null;
        if (type == typeof(ushort)) return ushort.TryParse(text, NumberStyles.Integer, inv, out var us) ? us : null;
        if (type == typeof(Int128)) return Int128.TryParse(text, NumberStyles.Integer, inv, out var i128) ? i128 : null;
        if (type == typeof(UInt128)) return UInt128.TryParse(text, NumberStyles.Integer, inv, out var u128) ? u128 : null;
        if (type == typeof(nint)) return nint.TryParse(text, NumberStyles.Integer, inv, out var ni) ? ni : null;
        if (type == typeof(nuint)) return nuint.TryParse(text, NumberStyles.Integer, inv, out var nu) ? nu : null;
        if (type == typeof(double)) return double.TryParse(text, NumberStyles.Float, inv, out var d) ? d : null;
        if (type == typeof(float)) return float.TryParse(text, NumberStyles.Float, inv, out var f) ? f : null;
        if (type == typeof(decimal)) return decimal.TryParse(text, NumberStyles.Number, inv, out var m) ? m : null;
        if (type == typeof(Guid)) return Guid.TryParse(text, out var g) ? g : null;
        if (type == typeof(DateTime)) return DateTime.TryParse(text, inv, DateTimeStyles.RoundtripKind, out var dt) ? dt : null;
        if (type == typeof(DateTimeOffset)) return DateTimeOffset.TryParse(text, inv, DateTimeStyles.RoundtripKind, out var dto) ? dto : null;
        if (type == typeof(TimeSpan)) return TimeSpan.TryParse(text, inv, out var ts) ? ts : null;
        if (type == typeof(DateOnly)) return DateOnly.TryParse(text, inv, DateTimeStyles.None, out var d1) ? d1 : null;
        if (type == typeof(TimeOnly)) return TimeOnly.TryParse(text, inv, DateTimeStyles.None, out var t1) ? t1 : null;
        if (type.IsEnum) return Enum.TryParse(type, text, ignoreCase: false, out var e) && IsKnown(type, e!) ? e : null;
        return null;
    }
}

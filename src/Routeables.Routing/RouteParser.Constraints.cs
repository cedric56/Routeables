using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Routeables.Routing;

// The constraints of a placeholder: the built-in ones, the registered types, and the reading of a value as its type.
public static partial class RouteParser
{
    private static readonly string[] BuiltInConstraints = ["string", "int", "long", "bool", "guid", "decimal", "double"];

    private static bool IsBuiltIn(string type)
        => type is "" or "?" || Array.Exists(BuiltInConstraints, c => string.Equals(c, type, StringComparison.OrdinalIgnoreCase));

    // The registered type of a constraint. A built-in name never refers to a registered type: an application type named Guid, String or Double
    // must not turn {id:guid} into a JSON value (reach it by its full name, {id:Sales.Guid}).
    private static bool TryGetSerializer(string type, out JsonTypeInfo info)
    {
        if (IsBuiltIn(type)) { info = null!; return false; }
        return SerializationInfos.TryGetValue(type, out info!);
    }

    private static bool IsKnownConstraint(string type)
        => IsBuiltIn(type) || SerializationInfos.ContainsKey(type);

    /// <summary>
    /// Converts a raw string parameter value into the specified target type constraint.
    /// </summary>
    private static bool TryConvert(string raw, string type, out object? value)
    {
        value = null;

        if (TryGetSerializer(type, out var info))
        {
            try { value = JsonSerializer.Deserialize(raw, info); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Catch more than JsonException: an application converter, or a type the info cannot read, may throw anything. An address that
                // cannot be read as the type does not match; it must not throw out of the route search, which runs for every address.
                return false;
            }
            return value is not null;
        }

        var inv = CultureInfo.InvariantCulture;
        switch (type.ToLowerInvariant())
        {
            case "" or "string" or "?": value = raw; return true;
            // The usual spellings are read (+42, 007, True, an upper-case Guid, 1.50, and 1e5 for a double: a decimal takes no exponent), and the router writes the address back in the one text
            // Build writes (Canonical), so that every spelling is one page for the history and RouteAddress. A number has no spaces, a Guid has its
            // dashes and no braces.
            case "int": { int v = 0; var ok = int.TryParse(raw, NumberStyles.AllowLeadingSign, inv, out v); value = v; return ok; }
            case "long": { long v = 0; var ok = long.TryParse(raw, NumberStyles.AllowLeadingSign, inv, out v); value = v; return ok; }
            // TryParse ignores spaces and a trailing null character: the text must be the one word, so that "true%00" is not a bool.
            case "bool": { var ok = bool.TryParse(raw, out var v) && raw == raw.Trim() && !raw.Contains('\0'); value = v; return ok; }
            // TryParseExact trims spaces: the 36 characters of the text with its dashes are checked, so that " <guid>" is not a second address of the page.
            case "guid": { Guid v = default; var ok = raw.Length == 36 && Guid.TryParseExact(raw, "D", out v); value = v; return ok; }
            case "decimal": { decimal v = 0; var ok = decimal.TryParse(raw, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, inv, out v); value = v; return ok; }
            case "double": { double v = 0; var ok = double.TryParse(raw, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent, inv, out v) && double.IsFinite(v);
                if (v == 0) v = 0; // -0 is the number 0, which the address is written as: the page gets the same zero
                value = v; return ok; }
            default:
                value = null;
                return false; // unreachable: Bind rejects an unknown constraint first
        }
    }
}

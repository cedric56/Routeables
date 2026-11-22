using Routeables.Abstraction.Routing;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Routeables.Routing;

// The other direction: an address written from a template and values, and the query of an address.
public static partial class RouteParser
{
    /// <summary>
    /// Writes the route a template stands for: the placeholders are filled from <paramref name="values"/> (an object's properties or a
    /// dictionary of name to value; names are not case-sensitive). Numbers and dates are written with the invariant culture, a
    /// registered type (<c>{person:Person}</c>) as JSON, and each value is escaped. A missing optional value leaves its segment (or its
    /// query entry) out.
    /// </summary>
    /// <exception cref="ArgumentException">A required value is missing, or a constraint is unknown.</exception>
    /// <summary>How many of the placeholders of <paramref name="template"/> get a value from <paramref name="values"/> (an optional one left out does not count).</summary>
    internal static int UsedBy(string template, object? values)
    {
        var bag = ValuesOf(values);
        var parsed = ParseTemplate(template);
        return parsed.SegmentMatches.Concat(parsed.QueryMatches)
            .Count(m => m.Success && bag.TryGetValue(m.Groups["name"].Value, out var value) && value is not null && value is not string { Length: 0 });
    }

    internal static string Build(string template, object? values)
    {
        ArgumentNullException.ThrowIfNull(template);
        var bag = ValuesOf(values);
        var parsedTemplate = ParseTemplate(template);
        if (parsedTemplate.DuplicateName is { } twice)
            throw new ArgumentException($"The name {twice} is used twice in \"{template}\": a value cannot be written in the path and in the query.", nameof(template));
        // What is written must be readable: the router rejects a template whose catch-all is not the last segment.
        if (parsedTemplate.RestAt >= 0 && parsedTemplate.RestAt != parsedTemplate.Segments.Length - 1)
            throw new ArgumentException($"The catch-all placeholder {parsedTemplate.Segments[parsedTemplate.RestAt]} of \"{template}\" must be the last segment of the template.", nameof(template));
        if (OptionalNotLast(Split(template).path.Split('/', StringSplitOptions.RemoveEmptyEntries)) is { } notLast)
            throw new ArgumentException($"The optional placeholder {notLast} of \"{template}\" must be the last segment of the template.", nameof(template));

        string? Fill(Match m, string where)
        {
            var name = m.Groups["name"].Value;
            var type = m.Groups["type"].Value;
            if (!IsKnownConstraint(type))
                throw new ArgumentException($"The constraint \"{type}\" of {{{name}}} in \"{template}\" is unknown.", nameof(template));
            if (!bag.TryGetValue(name, out var value) || value is null || value is string { Length: 0 })
            {
                if (m.Groups["opt"].Success) return null;
                throw new ArgumentException($"The route \"{template}\" needs a value for {{{name}}} ({where}).", nameof(values));
            }

            var text = Format(value, type);
            // A value whose text is empty (a StringBuilder with nothing in it) counts as no value, like an empty string.
            if (text.Length == 0)
            {
                if (m.Groups["opt"].Success) return null;
                throw new ArgumentException($"The route \"{template}\" needs a value for {{{name}}} ({where}).", nameof(values));
            }

            // Escaping would write a lone surrogate as U+FFFD, and the value read back would not be the one given.
            if (HasLoneSurrogate(text))
                throw new ArgumentException($"The value for {{{name}}} in \"{template}\" is not valid Unicode text (a surrogate without its pair).", nameof(values));
            if (where == "path" && HasDotSegment(text))
                throw new ArgumentException($"The value \"{text}\" cannot be a segment of \"{template}\" for {{{name}}}: a browser resolves \".\" and \"..\" to another address.", nameof(values));
            // What is written must be readable: the router would reject an address that its own template does not match.
            if (!TryConvert(text, type, out _))
                throw new ArgumentException(
                    $"The value \"{text}\" is not a valid {(type.Length == 0 ? "value" : type)} for {{{name}}} in \"{template}\".", nameof(values));
            return Uri.EscapeDataString(text);
        }

        var (path, query) = Split(template);
        var segments = new List<string>();
        string? skippedOptional = null;
        foreach (var segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            var m = Param.Match(segment);
            if (!m.Success) { segments.Add(segment); continue; }
            if (m.Groups["rest"].Success)
            {
                // {**path}: keep the slashes of the value and escape each segment.
                // As for other placeholders, a constraint that does not exist is refused whether or not there is a value (a template that cannot
                // be matched must not build an address for one value and be refused for another).
                var restType = m.Groups["type"].Value;
                if (!IsKnownConstraint(restType))
                    throw new ArgumentException($"The constraint \"{restType}\" of {{**{m.Groups["name"].Value}}} in \"{template}\" is unknown.", nameof(template));
                if (bag.TryGetValue(m.Groups["name"].Value, out var restValue) && restValue is not null
                    && Format(restValue, restType) is { Length: > 0 } restText)
                {
                    // The whole value must satisfy the constraint, so that it reads back.
                    if (restText.Split('/').Any(part => part.Length == 0))
                        throw new ArgumentException($"The value \"{restText}\" cannot be the rest of \"{template}\": an empty segment (a leading, trailing or doubled slash) is dropped from an address, so the value would not be read back.", nameof(values));
                    if (HasDotSegment(restText))
                        throw new ArgumentException($"The value \"{restText}\" cannot be the rest of \"{template}\": a browser resolves \".\" and \"..\" to another address.", nameof(values));
                    if (!TryConvert(restText, restType, out _))
                        throw new ArgumentException(
                            $"The value \"{restText}\" is not a valid {(restType.Length == 0 ? "value" : restType)} for {{**{m.Groups["name"].Value}}} in \"{template}\".", nameof(values));
                    // cat/{age?}/{**rest} with a rest but no age: the rest would be read as the age.
                    if (skippedOptional is { } skippedBefore)
                        throw new ArgumentException($"The route \"{template}\" cannot leave out {skippedBefore} and give a later value: give both, or neither.", nameof(values));
                    segments.AddRange(restText.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString));
                }
                continue;
            }
            if (Fill(m, "path") is { } text)
            {
                // cat/{age?}/{nick?} with a nick but no age: the nick would be read as the age.
                if (skippedOptional is { } skipped)
                    throw new ArgumentException($"The route \"{template}\" cannot leave out {skipped} and give a later value: give both, or neither.", nameof(values));
                segments.Add(text);
            }
            else if (m.Groups["opt"].Success)
            {
                skippedOptional ??= segment;
            }
        }

        var pairs = new List<string>();
        foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var m = Param.Match(part);
            if (!m.Success) { pairs.Add(part); continue; }
            if (Fill(m, "query") is { } text) pairs.Add($"{Uri.EscapeDataString(m.Groups["name"].Value)}={text}");
        }

        // A template made only of optional values, none given ("{tab?}"), is the root.
        var route = (path.StartsWith('/') || segments.Count == 0 ? "/" : string.Empty) + string.Join('/', segments);
        return pairs.Count == 0 ? route : route + "?" + string.Join('&', pairs);
    }

    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Reached with an object of values only through the overload of GoToAsync that is marked RequiresUnreferencedCode.")]
    private static Dictionary<string, object?> ValuesOf(object? values)
    {
        var bag = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        switch (values)
        {
            case null:
                break;
            case IReadOnlyDictionary<string, object?> map:
                foreach (var (key, value) in map) bag[key] = value;
                break;
            case IDictionary<string, object?> map:
                foreach (var (key, value) in map) bag[key] = value;
                break;
            case System.Collections.IDictionary map when map.GetType().IsGenericType && map.GetType().GetGenericArguments() is [var key, _] && key == typeof(string):
                // Dictionary<string, int>, Dictionary<string, string> and so on: the generic interfaces are not covariant, so none of the cases above matches.
                foreach (System.Collections.DictionaryEntry entry in map) bag[(string)entry.Key] = entry.Value;
                break;
            default:
                foreach (var property in values.GetType().GetProperties())
                    if (property.GetIndexParameters().Length == 0 && property.CanRead)
                        bag[property.Name] = property.GetValue(values);
                break;
        }

        return bag;
    }

    // A decimal keeps the trailing zeros it was written with (1.5m, 1.50m and 1.0m are the same number). Write it without them, so a number has one address.
    private static decimal Plain(decimal value) => value / 1.0000000000000000000000000000m;

    private static bool HasLoneSurrogate(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                i++;
            else if (char.IsSurrogate(text[i]))
                return true;
        }
        return false;
    }

    private static string Format(object value, string type)
    {
        if (TryGetSerializer(type, out var info))
            return JsonSerializer.Serialize(value, info);
        return value switch
        {
            string text => text,
            bool flag => flag ? "true" : "false",
            double zero when zero == 0 => "0", // -0 is the number 0: write one text
            float zeroF when zeroF == 0 => "0",
            decimal money => Plain(money).ToString(CultureInfo.InvariantCulture),
            // A date is written in ISO 8601, which reads the same everywhere: the invariant culture's "01/02/2026 03:04:05" is month first.
            DateTime moment => moment.ToString("o", CultureInfo.InvariantCulture),
            DateTimeOffset moment => moment.ToString("o", CultureInfo.InvariantCulture),
            DateOnly day => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            TimeOnly time => time.ToString("HH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty,
        };
    }

    /// <summary>The query of an address as name and value (the fragment is left out): the values a page that has no template receives.</summary>
    internal static IReadOnlyDictionary<string, string> QueryOf(string url)
    {
        return ParseQuery(Split(RouteAddress.WithoutFragment(url), template: false).query);
    }

    // Shared by every match of an address without a query: nobody can change it.
    private static readonly IReadOnlyDictionary<string, string> NoQuery = new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

    // One reading of a query for the whole library: see RouteAddress.ParseQuery.
    private static Dictionary<string, string> ParseQuery(string q) => RouteAddress.ParseQuery(q);

    /// <summary>
    /// A name or a value of a query string: <c>+</c> is a space and <c>%XX</c> is a character, as ASP.NET Core and Blazor read it (a form sent with
    /// GET writes a space as <c>+</c>). A real plus is <c>%2B</c>, which is what <see cref="Uri.EscapeDataString(string)"/> writes. A path segment is
    /// not a query: <c>+</c> stays a plus there.
    /// </summary>
    internal static string DecodeQuery(string text) => RouteAddress.DecodeQuery(text);
}

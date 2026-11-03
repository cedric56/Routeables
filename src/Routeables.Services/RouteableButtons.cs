using System.Text.RegularExpressions;

namespace Routeables.Services;

/// <summary>Helpers shared by the hosts that draw their message boxes themselves.</summary>
public static partial class RouteableButtons
{
    /// <summary>
    /// Splits buttons written together, one word per capital letter: <c>"YesNoCancel"</c> gives Yes, No and Cancel.
    /// An empty or <see langword="null"/> text gives no word.
    /// </summary>
    /// <param name="text">The buttons written together.</param>
    public static IReadOnlyList<string> Split(string? text)
        => string.IsNullOrEmpty(text) ? [] : [.. CapitalizedWordRegex().Matches(text).Select(m => m.Value)];

    [GeneratedRegex(@"\p{Lu}\p{Ll}*")]
    private static partial Regex CapitalizedWordRegex();
}

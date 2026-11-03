namespace Routeables.Avalonia;

/// <summary>String and enum helpers of the Avalonia host.</summary>
public static class Extensions
{
    /// <summary>Splits a text written together, one word per capital letter: <c>"YesNo"</c> gives Yes and No. See <see cref="Routeables.Services.RouteableButtons.Split"/>.</summary>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public static IEnumerable<string> SplitOnCapitals(this string text) => Routeables.Services.RouteableButtons.Split(text);

    /// <summary>Parses the text as an enum value, ignoring case; <paramref name="defaultValue"/> when it is not one.</summary>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public static T ToEnum<T>(this string value, T defaultValue = default) where T : struct
    {
        return Enum.TryParse(value, true, out T result) ? result : defaultValue;
    }
}

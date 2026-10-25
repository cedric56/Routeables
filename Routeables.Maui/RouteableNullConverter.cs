using System.Globalization;

namespace Routeables.Maui;

/// <summary>Converts a value to <see langword="true"/> when it is <see langword="null"/> or a blank string. The Avalonia one only tests for null.</summary>
public class RouteableNullConverter : IValueConverter
{
    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is null || (value is string s && string.IsNullOrWhiteSpace(s));
    }

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

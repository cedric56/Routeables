using Avalonia.Data.Converters;
using System.Globalization;

namespace Routeables.Avalonia;

/// <summary>Converts a value to <see langword="true"/> when it is <see langword="null"/>. Unlike the MAUI one, an empty or blank string is not null here.</summary>
public class RouteableNullConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value == null;

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

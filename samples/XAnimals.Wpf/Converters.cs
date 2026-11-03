using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using XAnimals.ViewModels;

namespace XAnimals.Wpf;

public sealed class BoolToBrushConverter : IValueConverter
{
    public Brush? TrueBrush { get; set; }
    public Brush? FalseBrush { get; set; }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is true ? TrueBrush : FalseBrush;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>The background of a card of the History page: the current entry, the ones ahead of it (forward), the past ones. Gets the HistoryItem.</summary>
public sealed class HistoryCardBrushConverter : IValueConverter
{
    public Brush? Current { get; set; }
    public Brush? Ahead { get; set; }
    public Brush? Past { get; set; }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is HistoryItem { IsCurrent: true } ? Current : value is HistoryItem { IsAhead: true } ? Ahead : Past;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

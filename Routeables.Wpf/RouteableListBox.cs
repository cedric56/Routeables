using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Routeables.Abstraction;

namespace Routeables.Wpf;

/// <summary>
/// The menu of the application: one item per entry of the main view model (<see cref="IMainRouteables.Routeables"/>), the
/// selected item is the current entry (<see cref="IMainRouteables.Routeable"/>). Its items are disabled while the menu is
/// locked. Its data context must be the main view model.
/// </summary>
public class RouteableListBox : ListBox
{
    /// <summary>Creates the menu.</summary>
    public RouteableListBox()
    {
        SetBinding(ItemsSourceProperty, new Binding(nameof(IMainRouteables.Routeables)));
        SetBinding(SelectedItemProperty, new Binding(nameof(IMainRouteables.Routeable)) { Mode = BindingMode.TwoWay });

        var name = new FrameworkElementFactory(typeof(TextBlock));
        name.SetBinding(TextBlock.TextProperty, new Binding(nameof(IRouteableEntry.DisplayName)));

        var description = new FrameworkElementFactory(typeof(TextBlock));
        description.SetBinding(TextBlock.TextProperty, new Binding(nameof(IRouteableEntry.Description)));
        description.SetBinding(VisibilityProperty, new Binding(nameof(IRouteableEntry.Description)) { Converter = new TextToVisibilityConverter() });
        description.SetValue(TextBlock.FontSizeProperty, 12.0);
        description.SetValue(OpacityProperty, 0.7);
        description.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);

        var texts = new FrameworkElementFactory(typeof(StackPanel));
        texts.AppendChild(name);
        texts.AppendChild(description);

        // Icon is a default member of the interface, which WPF does not see on the class: a converter reads it from the entry.
        var icon = new FrameworkElementFactory(typeof(ContentControl));
        icon.SetBinding(ContentControl.ContentProperty, new Binding { Converter = new IconConverter() });
        icon.SetValue(FontSizeProperty, 20.0);
        icon.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        icon.SetValue(MarginProperty, new Thickness(0, 0, 10, 0));

        var row = new FrameworkElementFactory(typeof(StackPanel));
        row.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
        row.AppendChild(icon);
        row.AppendChild(texts);

        ItemTemplate = new DataTemplate { VisualTree = row };

        // The Fluent theme (Application.ThemeMode) styles the controls by their exact type: a class derived from ListBox does not get
        // its style, and the menu stayed white (the colours of the classic theme) whatever the theme. The style of ListBox and of
        // ListBoxItem is taken from the resources, so that it follows the theme when it changes. Without a theme style, the default stays.
        SetResourceReference(StyleProperty, typeof(ListBox));

        // The entries are disabled while a page locks the menu.
        var item = new Style(typeof(ListBoxItem)) { BasedOn = Application.Current?.TryFindResource(typeof(ListBoxItem)) as Style };
        item.Setters.Add(new Setter(PaddingProperty, new Thickness(10)));
        // The Fluent style reads these two from the list through a binding that finds no list while the item is being created
        // (a Data Error 4 in the output): they are given here.
        item.Setters.Add(new Setter(HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
        item.Setters.Add(new Setter(VerticalContentAlignmentProperty, VerticalAlignment.Center));
        item.Setters.Add(new Setter(IsEnabledProperty, new Binding(nameof(IRouteableEntry.IsLock)) { Converter = new NotConverter() }));
        ItemContainerStyle = item;
    }
}

// WPF binds public properties only: the inverse of IsLock is computed here.
internal sealed class NotConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => value is not true;

    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => value is not true;
}

// HasDescription is a default member of the interface, which WPF does not see on the class: the text itself decides.
internal sealed class TextToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => throw new NotSupportedException();
}

// The icon the resolvers give to the entry (a glyph for WPF).
internal sealed class IconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => (value as IRouteableEntry)?.Icon;

    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => throw new NotSupportedException();
}

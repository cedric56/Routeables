using Routeables.Abstraction.Bases;
using System.Globalization;

namespace Routeables.Maui;

/// <summary>
/// The menu of the application: one row per entry of the menu view model (<see cref="IRouteableMenuViewModel.Entries"/>), the selected row is the
/// current entry (<see cref="IRouteableMenuViewModel.SelectedEntry"/>). Its rows are disabled while the menu is locked. Its binding context must be the
/// menu view model (it is, in a page of the application, when the page gets it from the container).
/// <code>
/// &lt;routeables:RouteableMenu BindingContext="{Binding MainViewModel}" /&gt;
/// </code>
/// </summary>
public class RouteableMenu : ContentView
{
    /// <summary>Creates the menu.</summary>
    public RouteableMenu()
    {
        var list = new CollectionView
        {
            SelectionMode = SelectionMode.Single,
            ItemTemplate = new DataTemplate(CreateRow)
        };
        list.SetBinding(ItemsView.ItemsSourceProperty, new Binding(nameof(IRouteableMenuViewModel.Entries)));
        list.SetBinding(SelectableItemsView.SelectedItemProperty, new Binding(nameof(IRouteableMenuViewModel.SelectedEntry), BindingMode.TwoWay));
        Content = list;
    }

    // Icon, Description and HasDescription are default members of the interface, which a binding does not see on the class: converters read
    // them from the entry itself.
    private static View CreateRow()
    {
        var icon = new Label { FontSize = 20, VerticalOptions = LayoutOptions.Center, WidthRequest = 28, HorizontalTextAlignment = TextAlignment.Center };
        icon.SetBinding(Label.TextProperty, new Binding(".", converter: EntryText.Icon));
        icon.SetBinding(IsVisibleProperty, new Binding(".", converter: EntryText.HasIcon));

        var name = new Label { FontAttributes = FontAttributes.Bold };
        name.SetBinding(Label.TextProperty, new Binding(nameof(IRouteableMenuEntry.DisplayName)));

        var description = new Label { FontSize = 12, Opacity = 0.7, LineBreakMode = LineBreakMode.WordWrap };
        description.SetBinding(Label.TextProperty, new Binding(".", converter: EntryText.Description));
        description.SetBinding(IsVisibleProperty, new Binding(".", converter: EntryText.HasDescription));

        var texts = new VerticalStackLayout { Spacing = 2, Children = { name, description } };
        var row = new HorizontalStackLayout { Spacing = 10, Padding = new Thickness(10), Children = { icon, texts } };

        // The rows are disabled while a page locks the menu.
        row.SetBinding(IsEnabledProperty, new Binding(nameof(IRouteableMenuEntry.IsLock), converter: EntryText.Not));
        return row;
    }
}

// What the rows need from an entry, read from the interface.
internal sealed class EntryText(Func<object?, object?> read) : IValueConverter
{
    internal static readonly EntryText Icon = new(v => (v as IRouteableMenuEntry)?.Icon as string);
    internal static readonly EntryText HasIcon = new(v => (v as IRouteableMenuEntry)?.Icon is string { Length: > 0 });
    internal static readonly EntryText Description = new(v => (v as IRouteableMenuEntry)?.Description);
    internal static readonly EntryText HasDescription = new(v => v is IRouteableMenuEntry entry && !string.IsNullOrWhiteSpace(entry.Description));
    internal static readonly EntryText Not = new(v => v is not true);

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => read(value);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

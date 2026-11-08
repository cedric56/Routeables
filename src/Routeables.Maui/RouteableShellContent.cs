using System.Globalization;

namespace Routeables.Maui;

/// <summary>
/// Represents a Shell content item with an additional description for display in the Shell flyout.
/// </summary>
public sealed class RouteableShellContent : ShellContent
{
    /// <summary>
    /// Gets or sets the descriptive text displayed below the item's title in the Shell flyout.
    /// </summary>
    public string? Description
    {
        get => (string?)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="Description"/> bindable property.
    /// </summary>
    public static readonly BindableProperty DescriptionProperty =
        BindableProperty.Create(
            nameof(Description),
            typeof(string),
            typeof(RouteableShellContent));

    /// <summary>
    /// Converts a string value into a visibility state.
    /// </summary>
    /// <remarks>
    /// Returns <see langword="false"/> for <see langword="null"/>, empty or whitespace-only strings,
    /// allowing the description label to collapse when no description is provided.
    /// </remarks>
    sealed class StringNotNullOrEmptyConverter : IValueConverter
    {
        /// <summary>
        /// Determines whether the supplied string contains non-whitespace characters.
        /// </summary>
        /// <param name="value">The value to evaluate.</param>
        /// <param name="targetType">The binding target property type.</param>
        /// <param name="parameter">An optional converter parameter.</param>
        /// <param name="culture">The culture to use in the conversion.</param>
        /// <returns><see langword="true"/> if the string is not null, empty or whitespace; otherwise, <see langword="false"/>.</returns>
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => !string.IsNullOrWhiteSpace(value as string);

        /// <summary>
        /// Converts a visibility value back to a string.
        /// </summary>
        /// <param name="value">The value produced by the binding target.</param>
        /// <param name="targetType">The binding source property type.</param>
        /// <param name="parameter">An optional converter parameter.</param>
        /// <param name="culture">The culture to use in the conversion.</param>
        /// <returns>This conversion is not supported.</returns>
        /// <exception cref="NotSupportedException">Always thrown because the conversion is one-way.</exception>
        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    /// <summary>
    /// Creates the data template used to display a <see cref="RouteableShellContent"/> in the Shell flyout.
    /// </summary>
    /// <remarks>
    /// The template displays the item's icon and title, followed by its description when one is available.
    /// The description label is collapsed when its value is null, empty or whitespace.
    /// </remarks>
    /// <returns>A <see cref="DataTemplate"/> for rendering Shell flyout items.</returns>
    public static DataTemplate GetTemplate()
    {
        return new DataTemplate(() =>
        {
            var icon = new Image
            {
                HeightRequest = 18,
                WidthRequest = 18,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalOptions = LayoutOptions.Center
            };
            icon.SetBinding(Image.SourceProperty, nameof(FlyoutIcon));

            var title = new Label
            {
                FontAttributes = FontAttributes.Bold,
                VerticalOptions = LayoutOptions.Center
            };
            title.SetBinding(Label.TextProperty, nameof(Title));

            var description = new Label
            {
                FontSize = 7
            };

            description.SetBinding(Label.TextProperty, nameof(Description));

            description.SetBinding(
                IsVisibleProperty,
                new Binding(nameof(Description), converter: new StringNotNullOrEmptyConverter()));

            var labels = new VerticalStackLayout
            {
                Children = { title, description }
            };

            var grid = new Grid
            {
                ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            }
            };

            grid.Add(icon, 0, 0);
            grid.Add(labels, 1, 0);

            return grid;
        });
    }
}

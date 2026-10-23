using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Routeables.Abstraction;
using Routeables.Routing;
using System.Collections.ObjectModel;

namespace Routeables.Avalonia;

/// <summary>
/// Represents a specialized Avalonia <see cref="ListBox"/> designed to bind directly against 
/// <see cref="IMainRouteables"/> collections and manage routeable view routing items.
/// </summary>
public class RouteableListBox : ListBox
{
    /// <summary>
    /// Gets the style key override to preserve the default <see cref="ListBox"/> theme styling.
    /// </summary>
    protected override Type StyleKeyOverride => typeof(ListBox);

    /// <summary>
    /// Initializes a new instance of the <see cref="RouteableListBox"/> class, configuring 
    /// items source bindings, selection bindings, item templates, and container styles.
    /// </summary>
    public RouteableListBox()
    {
        // Bindings against the DataContext
        Bind(ItemsControl.ItemsSourceProperty, CompiledBinding.Create<IMainRouteables, ObservableCollection<IRouteableEntry>>(r => r.Routeables));
        Bind(SelectingItemsControl.SelectedItemProperty,
            CompiledBinding.Create<IMainRouteables, IRouteableEntry?>(r => r.Routeable, mode: BindingMode.TwoWay));

        // Item template (live bindings)
        ItemTemplate = new FuncDataTemplate<RouteableEntry>((_, _) =>
        {
            var icon = new ContentControl { VerticalAlignment = VerticalAlignment.Center };
            icon.Bind(ContentControl.ContentProperty,
                CompiledBinding.Create<IRouteableEntry, object?>(r => r.Icon));

            var name = new TextBlock();
            name.Bind(TextBlock.TextProperty,
                CompiledBinding.Create<IRouteableEntry, string>(r => r.DisplayName));

            var description = new TextBlock
            {
                FontSize = 12,
                Opacity = 0.7,
                TextWrapping = TextWrapping.Wrap
            };
            description.Bind(TextBlock.TextProperty,
                CompiledBinding.Create<IRouteableEntry, string?>(r => r.Description));
            description.Bind(Visual.IsVisibleProperty,
                CompiledBinding.Create<IRouteableEntry, bool>(r => r.HasDescription));

            var texts = new StackPanel { Spacing = 2, Children = { name, description } };

            var stack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                Children = { icon, texts }
            };
            return stack;
        });

        // Item style
        Styles.Add(new Style(x => x.OfType<ListBoxItem>())
        {
            Setters =
        {
            new Setter(TemplatedControl.PaddingProperty, new Thickness(10)),
            new Setter(TemplatedControl.IsEnabledProperty, CompiledBinding.Create<RouteableEntry, bool>(r => r.InverseIsLock))
        }
        });
    }
}

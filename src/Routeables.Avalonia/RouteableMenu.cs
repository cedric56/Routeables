using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Routeables.Abstraction.Bases;
using Routeables.Routing;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Routeables.Avalonia;

/// <summary>
/// Represents a specialized Avalonia <see cref="ListBox"/> designed to bind directly against 
/// <see cref="IMainViewModel"/> collections and manage routeable view routing items.
/// </summary>
public class RouteableMenu : ListBox
{
    /// <summary>
    /// Gets the style key override to preserve the default <see cref="ListBox"/> theme styling.
    /// </summary>
    protected override Type StyleKeyOverride => typeof(ListBox);

    private INotifyPropertyChanged? _source;

    /// <inheritdoc />
    protected override void OnDataContextBeginUpdate()
    {
        if (_source is not null)
            _source.PropertyChanged -= OnMainViewModelChanged;
        _source = null;
        base.OnDataContextBeginUpdate();
    }

    /// <inheritdoc />
    protected override void OnDataContextEndUpdate()
    {
        base.OnDataContextEndUpdate();
        if (DataContext is IMainViewModel and INotifyPropertyChanged source)
        {
            _source = source;
            source.PropertyChanged += OnMainViewModelChanged;
        }
    }

    // The entry that was clicked stays selected in the list when the page refused to be left (CanLeaveAsync) and the view model took
    // the previous entry back: the binding does not always push that value to the list. Whenever the view model announces its entry,
    // the list shows it, once the current update of the selection is over.
    private void OnMainViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(IMainViewModel.SelectedEntry))
            return;

        Dispatcher.UIThread.Post(() =>
        {
            if (DataContext is IMainViewModel main && !ReferenceEquals(SelectedItem, main.SelectedEntry))
                SetCurrentValue(SelectingItemsControl.SelectedItemProperty, main.SelectedEntry);
        }, DispatcherPriority.Background);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RouteableMenu"/> class, configuring 
    /// items source bindings, selection bindings, item templates, and container styles.
    /// </summary>
    public RouteableMenu()
    {
        // Bindings against the DataContext
        Bind(ItemsControl.ItemsSourceProperty, CompiledBinding.Create<IMainViewModel, ObservableCollection<IRouteableMenuEntry>>(r => r.Entries));
        Bind(SelectingItemsControl.SelectedItemProperty,
            CompiledBinding.Create<IMainViewModel, IRouteableMenuEntry?>(r => r.SelectedEntry, mode: BindingMode.TwoWay));

        // Item template (live bindings)
        ItemTemplate = new FuncDataTemplate<RouteableMenuEntry>((_, _) =>
        {
            var icon = new ContentControl { VerticalAlignment = VerticalAlignment.Center };
            icon.Bind(ContentControl.ContentProperty,
                CompiledBinding.Create<IRouteableMenuEntry, object?>(r => r.Icon));

            var name = new TextBlock();
            name.Bind(TextBlock.TextProperty,
                CompiledBinding.Create<IRouteableMenuEntry, string>(r => r.DisplayName));

            var description = new TextBlock
            {
                FontSize = 12,
                Opacity = 0.7,
                TextWrapping = TextWrapping.Wrap
            };
            description.Bind(TextBlock.TextProperty,
                CompiledBinding.Create<IRouteableMenuEntry, string?>(r => r.Description));
            description.Bind(Visual.IsVisibleProperty,
                CompiledBinding.Create<IRouteableMenuEntry, bool>(r => r.HasDescription));

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
            new Setter(TemplatedControl.IsEnabledProperty, CompiledBinding.Create<RouteableMenuEntry, bool>(r => r.InverseIsLock))
        }
        });
    }
}

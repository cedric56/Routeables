using Avalonia;
using Avalonia.Automation.Peers;
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
using Orientation = Avalonia.Layout.Orientation;


namespace Routeables.Avalonia;

/// <summary>
/// Represents a specialized Avalonia <see cref="ListBox"/> designed to bind directly against 
/// <see cref="IRouteableMenuViewModel"/> collections and manage routeable view routing items.
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
            _source.PropertyChanged -= OnMenuViewModelChanged;
        _source = null;
        base.OnDataContextBeginUpdate();
    }

    /// <inheritdoc />
    protected override void OnDataContextEndUpdate()
    {
        base.OnDataContextEndUpdate();
        if (DataContext is IRouteableMenuViewModel and INotifyPropertyChanged source)
        {
            _source = source;
            source.PropertyChanged += OnMenuViewModelChanged;
        }
    }

    // The entry that was clicked stays selected in the list when the page refused to be left (CanLeaveAsync) and the view model took
    // the previous entry back: the binding does not always push that value to the list. Whenever the view model announces its entry,
    // the list shows it, once the current update of the selection is over.
    private void OnMenuViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(IRouteableMenuViewModel.SelectedEntry))
            return;

        Dispatcher.UIThread.Post(() =>
        {
            if (DataContext is IRouteableMenuViewModel main && !ReferenceEquals(SelectedItem, main.SelectedEntry))
                SetCurrentValue(SelectingItemsControl.SelectedItemProperty, main.SelectedEntry);
        }, DispatcherPriority.Background);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new RouteableMenuAutomationPeer(this);

    /// <summary>
    /// The accessibility peer of the menu. Avalonia's AT-SPI bridge (Linux) shows the entries of a list as the list's own children
    /// and keeps that list until the peer of the list reports a change; the entries are created by the panel inside the list, whose
    /// changes it does not follow. Without this, screen readers and test tools on Linux see only the entries that existed when the
    /// menu was first read (often the selected one). Each entry created, removed or moved reports a change of the list, once per
    /// layout pass.
    /// </summary>
    private sealed class RouteableMenuAutomationPeer : SelectingItemsControlAutomationPeer
    {
        private bool _pending;

        public RouteableMenuAutomationPeer(RouteableMenu owner)
            : base(owner)
        {
            owner.ContainerPrepared += (_, _) => ScheduleInvalidate();
            owner.ContainerClearing += (_, _) => ScheduleInvalidate();
            owner.ContainerIndexChanged += (_, _) => ScheduleInvalidate();
        }

        private void ScheduleInvalidate()
        {
            if (_pending)
                return;
            _pending = true;
            Dispatcher.UIThread.Post(() =>
            {
                _pending = false;
                InvalidateChildren();
            }, DispatcherPriority.Background);
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RouteableMenu"/> class, configuring 
    /// items source bindings, selection bindings, item templates, and container styles.
    /// </summary>
    public RouteableMenu()
    {
        // Bindings against the DataContext
        Bind(ItemsControl.ItemsSourceProperty, CompiledBinding.Create<IRouteableMenuViewModel, ObservableCollection<IRouteableMenuEntry>>(r => r.Entries));
        Bind(SelectingItemsControl.SelectedItemProperty,
            CompiledBinding.Create<IRouteableMenuViewModel, IRouteableMenuEntry?>(r => r.SelectedEntry, mode: BindingMode.TwoWay));

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

using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Routeables.Abstraction;

namespace Routeables.Uno;

/// <summary>
/// The content of an entry of a <see cref="RouteableListView"/>: the icon, the name and, under it, the description.
/// It follows its entry: a new name appears at once, and a locked entry is disabled.
/// </summary>
public partial class RouteableMenuItem : ContentControl
{
    /// <summary>The entry shown.</summary>
    public static readonly DependencyProperty ItemProperty =
        DependencyProperty.Register(nameof(Item), typeof(IRouteableEntry), typeof(RouteableMenuItem),
            new PropertyMetadata(null, (d, e) => ((RouteableMenuItem)d).OnItemChanged(e.OldValue as IRouteableEntry, e.NewValue as IRouteableEntry)));

    private readonly ContentControl _icon = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _name = new();
    private readonly TextBlock _description = new() { FontSize = 12, Opacity = 0.7, TextWrapping = TextWrapping.Wrap };
    private bool _loaded;

    /// <summary>Creates the item.</summary>
    public RouteableMenuItem()
    {
        var texts = new StackPanel { Spacing = 2 };
        texts.Children.Add(_name);
        texts.Children.Add(_description);

        var stack = new StackPanel { Orientation = Microsoft.UI.Xaml.Controls.Orientation.Horizontal, Spacing = 10, Padding = new Thickness(10) };
        stack.Children.Add(_icon);
        stack.Children.Add(texts);
        Content = stack;

        // The entry holds this control through its event: it is followed while the control is in the tree only.
        Loaded += (_, _) =>
        {
            _loaded = true;
            Watch(Item);
            Refresh(Item);
        };
        Unloaded += (_, _) =>
        {
            Unwatch(Item);
            _loaded = false;
        };
    }

    /// <summary>The entry shown.</summary>
    public IRouteableEntry? Item
    {
        get => (IRouteableEntry?)GetValue(ItemProperty);
        set => SetValue(ItemProperty, value);
    }

    private void OnItemChanged(IRouteableEntry? oldItem, IRouteableEntry? newItem)
    {
        Unwatch(oldItem);
        Refresh(newItem);
        Watch(newItem);
    }

    private void Watch(IRouteableEntry? item)
    {
        if (_loaded && item is INotifyPropertyChanged notifier)
        {
            notifier.PropertyChanged -= OnItemPropertyChanged;
            notifier.PropertyChanged += OnItemPropertyChanged;
        }
    }

    private void Unwatch(IRouteableEntry? item)
    {
        if (item is INotifyPropertyChanged notifier)
            notifier.PropertyChanged -= OnItemPropertyChanged;
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // An entry can change on the thread of a route change.
        if (DispatcherQueue is { } queue && !queue.HasThreadAccess)
            queue.TryEnqueue(() => Refresh(Item));
        else
            Refresh(Item);
    }

    private void Refresh(IRouteableEntry? item)
    {
        _name.Text = item?.DisplayName ?? string.Empty;
        _description.Text = item?.Description ?? string.Empty;
        _description.Visibility = item?.HasDescription == true ? Visibility.Visible : Visibility.Collapsed;
        _icon.Content = item?.Icon;
        _icon.Visibility = item?.Icon is null ? Visibility.Collapsed : Visibility.Visible;

        // A locked entry cannot be chosen: the container of the list is disabled with its content.
        var enabled = item is null || !item.IsLock;
        IsEnabled = enabled;
        if (FindContainer() is { } container)
            container.IsEnabled = enabled;
    }

    private ListViewItem? FindContainer()
    {
        DependencyObject? current = this;
        while (current is not null)
        {
            if (current is ListViewItem container)
                return container;
            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}

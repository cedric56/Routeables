using System.Windows;
using System.Windows.Controls;
using Routeables.Abstraction;
using Routeables.Regions;

namespace Routeables.Wpf;

/// <summary>
/// Shows every view of a region, not only the current one: the pages that were routed to the region and not closed,
/// side by side in rows and columns (a <see cref="WrapPanel"/> unless you set another <see cref="ItemsControl.ItemsPanel"/>).
/// Put it in a <see cref="ScrollViewer"/> when the zone can outgrow its space.
/// </summary>
public class RouteableRegionItems : ItemsControl
{
    /// <summary>Identifies <see cref="Region"/>.</summary>
    public static readonly DependencyProperty RegionProperty =
        DependencyProperty.Register(nameof(Region), typeof(string), typeof(RouteableRegionItems),
            new PropertyMetadata(null, (d, _) => ((RouteableRegionItems)d).Reconnect()));

    private RegionItemsBinding? _binding;
    private bool _active;

    static RouteableRegionItems()
    {
        // The views are laid out in rows and columns by default; a style or an ItemsPanel in XAML replaces it.
        ItemsPanelProperty.OverrideMetadata(typeof(RouteableRegionItems),
            new FrameworkPropertyMetadata(new ItemsPanelTemplate(new FrameworkElementFactory(typeof(WrapPanel)))));
    }

    /// <summary>Creates the control.</summary>
    public RouteableRegionItems()
    {
        Loaded += (_, _) => Connect();
        Unloaded += (_, _) => Disconnect();
    }

    /// <summary>Gets or sets the name of the region; empty for the default one.</summary>
    public string? Region
    {
        get => (string?)GetValue(RegionProperty);
        set => SetValue(RegionProperty, value);
    }

    /// <summary>Gets or sets the container to read the region from; the one of the application when not set.</summary>
    public IServiceProvider? Services { get; set; }

    private void Reconnect()
    {
        if (_binding is null) return;

        Disconnect();
        Connect();
    }

    private void Connect()
    {
        if (_binding is not null) return;

        var provider = Services ?? ServiceLocator.Services;
        if (provider is null) return;

        // Set before the binding exists: it gives the views of now as soon as it is built.
        _active = true;
        _binding = new RegionItemsBinding(provider.GetRouter(Region), Show);
    }

    // A view has one parent: they are released when the control is unloaded, so another control can display them.
    private void Disconnect()
    {
        _active = false;
        _binding?.Dispose();
        _binding = null;
        ItemsSource = null;
    }

    private void Show(IReadOnlyList<object> views)
    {
        if (Dispatcher.CheckAccess())
            Apply(views);
        else
            Dispatcher.InvokeAsync(() => Apply(views));
    }

    private void Apply(IReadOnlyList<object> views)
    {
        // A late post after the control was disconnected must not show the views again.
        if (!_active) return;

        foreach (var view in views)
            if (view is DependencyObject shown && LogicalTreeHelper.GetParent(shown) is { } holder && !ReferenceEquals(holder, this))
                throw new InvalidOperationException(
                    $"{view.GetType().Name} is already displayed by {holder.GetType().Name}, so region '{Region ?? "(default)"}' cannot show it too. " +
                    "A page shown by a region must not also host its own current view: let the region display it.");

        // A copy: the list belongs to the binding, and the control must not follow a list that is replaced.
        // The views are released first: one that stays is a child of the control until its container is cleared.
        ItemsSource = null;
        ItemsSource = views.ToList();
    }
}

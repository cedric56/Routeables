using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Routeables.Abstraction;
using Routeables.Regions;

namespace Routeables.Uno;

/// <summary>
/// Like <see cref="RouteableRegion"/>, but shows <b>every</b> view of the region instead of the current one: the views of the
/// history of the router registered under <see cref="Region"/> (<c>services.AddRegion("adopt")</c>), oldest first, up to the
/// current one, in rows and columns: a row holds as many views of at least <see cref="MinItemWidth"/> as fit, the others go to the
/// row below (the zone scrolls when the rows do not fit). Routing the region adds a view, <c>PopAsync</c> and <c>BackAsync</c>
/// remove the last. Nobody assigns a view to it: whoever routes the region only names it.
/// <code>
/// &lt;routeables:RouteableRegionItems Region="adopt" /&gt;
/// </code>
/// </summary>
public partial class RouteableRegionItems : ContentControl
{
    /// <summary>The name given to <c>AddRegion</c>; empty or <see langword="null"/> shows the views of the default router.</summary>
    public static readonly DependencyProperty RegionProperty =
        DependencyProperty.Register(nameof(Region), typeof(string), typeof(RouteableRegionItems),
            new PropertyMetadata(null, (d, _) => ((RouteableRegionItems)d).Reconnect()));

    /// <summary>The space between two views.</summary>
    public static readonly DependencyProperty SpacingProperty =
        DependencyProperty.Register(nameof(Spacing), typeof(double), typeof(RouteableRegionItems), new PropertyMetadata(8.0));

    /// <summary>The least width of a view: a row holds as many as fit, the others go to the next row.</summary>
    public static readonly DependencyProperty MinItemWidthProperty =
        DependencyProperty.Register(nameof(MinItemWidth), typeof(double), typeof(RouteableRegionItems), new PropertyMetadata(200.0));

    private RegionItemsBinding? _binding;
    private bool _active;

    /// <summary>Creates the region.</summary>
    public RouteableRegionItems()
    {
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
        Loaded += (_, _) => Connect();
        Unloaded += (_, _) => Disconnect();
    }

    /// <summary>The name given to <c>AddRegion</c>; <see langword="null"/> shows the views of the default router.</summary>
    public string? Region
    {
        get => (string?)GetValue(RegionProperty);
        set => SetValue(RegionProperty, value);
    }

    /// <summary>The space between two views.</summary>
    public double Spacing
    {
        get => (double)GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    /// <summary>The least width of a view: a row holds as many as fit, the others go to the next row.</summary>
    public double MinItemWidth
    {
        get => (double)GetValue(MinItemWidthProperty);
        set => SetValue(MinItemWidthProperty, value);
    }

    /// <summary>
    /// Where the router is found. By default the provider of the running host (<c>Application.UseRouteables</c>); set it to show the
    /// regions of another one.
    /// </summary>
    public IServiceProvider? Services { get; set; }

    private void Reconnect()
    {
        if (_binding is null)
            return;

        Disconnect();
        Connect();
    }

    private void Connect()
    {
        if (_binding is not null) return;

        var provider = Services ?? ServiceLocator.Services;
        if (provider is null) return;

        var name = string.IsNullOrEmpty(Region) ? null : Region;
        _active = true;
        _binding = new RegionItemsBinding(provider.GetRouter(name), Show);
    }

    // A view has one parent: they are released when the control leaves the tree, so another control can display them.
    private void Disconnect()
    {
        _active = false;
        _binding?.Dispose();
        _binding = null;
        Release();
    }

    private void Release()
    {
        if (Content is ScrollViewer { Content: ItemsRepeater repeater })
            repeater.ItemsSource = null;

        Content = null;
    }

    private void Show(IReadOnlyList<object> views)
    {
        if (DispatcherQueue.HasThreadAccess)
            Apply(views);
        else
            DispatcherQueue.TryEnqueue(() => Apply(views));
    }

    private void Apply(IReadOnlyList<object> views)
    {
        // A late post after the control was disconnected must not show the views again.
        if (!_active) return;

        // The views leave the old layout before they enter the new one.
        Release();

        // A view is its own element: the repeater lays the views out in a grid that wraps, and scrolls when the rows do not fit.
        var repeater = new ItemsRepeater
        {
            Layout = new UniformGridLayout { MinItemWidth = MinItemWidth, MinColumnSpacing = Spacing, MinRowSpacing = Spacing },
            ItemsSource = views.OfType<FrameworkElement>().ToList()
        };
        Content = new ScrollViewer { Content = repeater };
    }
}

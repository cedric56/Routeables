using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Routeables.Abstraction;
using Routeables.Regions;

namespace Routeables.Avalonia;

/// <summary>
/// Like <see cref="RouteableRegion"/>, but shows <b>every</b> view of the region instead of the current one: the views of the
/// history of the router registered under <see cref="Region"/> (<c>services.AddRegion("stack")</c>), oldest first, up to the
/// current one. Routing the region adds a view, <c>PopAsync</c> and <c>BackAsync</c> remove the last.
/// <para>
/// It is an <see cref="ItemsControl"/>: give it a panel to choose how the views are laid out (a <c>StackPanel</c>, a
/// <c>WrapPanel</c>, a <c>UniformGrid</c>...). Nobody assigns a view to it, whoever routes the region only names it.
/// </para>
/// <code>
/// &lt;routeables:RouteableRegionItems Region="stack"&gt;
///     &lt;routeables:RouteableRegionItems.ItemsPanel&gt;&lt;ItemsPanelTemplate&gt;&lt;WrapPanel /&gt;&lt;/ItemsPanelTemplate&gt;&lt;/routeables:RouteableRegionItems.ItemsPanel&gt;
/// &lt;/routeables:RouteableRegionItems&gt;
/// </code>
/// </summary>
public class RouteableRegionItems : ItemsControl
{
    /// <summary>The <see cref="Region"/> property.</summary>
    public static readonly StyledProperty<string?> RegionProperty =
        AvaloniaProperty.Register<RouteableRegionItems, string?>(nameof(Region));

    private RegionItemsBinding? _binding;
    private bool _active;

    /// <summary>The name given to <c>AddRegion</c>; <see langword="null"/> shows the views of the default router.</summary>
    public string? Region
    {
        get => GetValue(RegionProperty);
        set => SetValue(RegionProperty, value);
    }

    /// <summary>
    /// Where the router is found. By default the provider of the running host (<c>UseRouteables</c>); set it to show the
    /// regions of another one.
    /// </summary>
    public IServiceProvider? Services { get; set; }

    // The control keeps the style of ItemsControl.
    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(ItemsControl);

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Connect();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Disconnect();
        base.OnDetachedFromVisualTree(e);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == RegionProperty && _binding is not null)
        {
            Disconnect();
            Connect();
        }
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

    // A view has one parent: they are released when the control leaves the tree, so another control can display them.
    private void Disconnect()
    {
        _active = false;
        _binding?.Dispose();
        _binding = null;
        ItemsSource = null;
    }

    private void Show(IReadOnlyList<object> views)
    {
        if (Dispatcher.UIThread.CheckAccess())
            Apply(views);
        else
            Dispatcher.UIThread.Post(() => Apply(views));
    }

    private void Apply(IReadOnlyList<object> views)
    {
        // A late post after the control was disconnected must not show the views again.
        if (!_active) return;

        foreach (var view in views)
            if (view is Visual shown && shown.GetVisualParent() is { } holder && !this.IsVisualAncestorOf(shown))
                throw new InvalidOperationException(
                    $"{view.GetType().Name} is already displayed by {holder.GetType().Name}, so region '{Region ?? "(default)"}' cannot show it too. " +
                    "A page shown by a region must not also host its own current view: let the region display it.");

        // A copy: the list belongs to the binding, and the control must not follow a list that is replaced.
        ItemsSource = views.ToList();
    }
}

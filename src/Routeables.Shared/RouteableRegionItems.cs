// Uno is what is left when no other host is defined: Routeables.Uno links this file without defining a symbol.
#if !WPF && !AVALONIA && !MAUI && !WINUI3
#define ROUTEABLE_UNO
#endif
#if WPF
using System.Windows;
using System.Windows.Controls;
#elif AVALONIA
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
#elif MAUI
#else
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
#endif
using Routeables.Regions;

// Shared source for every host: Routeables.Wpf, Routeables.Avalonia and Routeables.Maui (which define WPF, AVALONIA or MAUI), Routeables.Uno and Routeables.WinUI3.
#if WPF
namespace Routeables.Wpf;
#elif AVALONIA
namespace Routeables.Avalonia;
#elif MAUI
namespace Routeables.Maui;
#elif WINUI3
namespace Routeables.WinUI3;
#else
namespace Routeables.Uno;
#endif

#if WPF
/// <summary>
/// Shows every view of a region, not only the current one: the pages that were routed to the region and not closed,
/// side by side in rows and columns (a <see cref="WrapPanel"/> unless you set another <see cref="ItemsControl.ItemsPanel"/>).
/// Put it in a <see cref="ScrollViewer"/> when the zone can outgrow its space.
/// </summary>
public partial class RouteableRegionItems : ItemsControl
#elif AVALONIA
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
public partial class RouteableRegionItems : ItemsControl
#elif MAUI
/// <summary>
/// Like <see cref="RouteableRegion"/>, but shows <b>every</b> view of the region instead of the current one: the views of the
/// history of the router registered under <see cref="Region"/> (<c>services.AddRegion("adopt")</c>), oldest first, up to the
/// current one, in rows and columns: a view is <see cref="MinItemWidth"/> wide, and the next one goes to the row below when the zone
/// is full (the zone scrolls when the rows do not fit). Routing the region adds a view, <c>PopAsync</c> and <c>BackAsync</c>
/// remove the last. Nobody assigns a view to it: whoever routes the region only names it.
/// <code>
/// &lt;routeables:RouteableRegionItems Region="adopt" /&gt;
/// </code>
/// </summary>
public partial class RouteableRegionItems : ContentView
#else
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
#endif
{
#if WPF
    /// <summary>The automation peer: a group named after what the control shows (see <c>RouteableRegionItemsAutomationPeer</c>).</summary>
    protected override System.Windows.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => new Automation.RouteableRegionItemsAutomationPeer(this);

    /// <summary>Identifies <see cref="Region"/>.</summary>
    public static readonly DependencyProperty RegionProperty =
        DependencyProperty.Register(nameof(Region), typeof(string), typeof(RouteableRegionItems),
            new PropertyMetadata(null, (d, _) => ((RouteableRegionItems)d).Reconnect()));

    static RouteableRegionItems()
    {
        // The views are laid out in rows and columns by default; a style or an ItemsPanel in XAML replaces this.
        ItemsPanelProperty.OverrideMetadata(typeof(RouteableRegionItems),
            new FrameworkPropertyMetadata(new ItemsPanelTemplate(new FrameworkElementFactory(typeof(WrapPanel)))));
    }

    /// <summary>Creates the control.</summary>
    public RouteableRegionItems()
    {
        Loaded += (_, _) => Connect();
        Unloaded += (_, _) => Disconnect();
    }
#elif AVALONIA
    /// <summary>The automation peer: a group named after what the control shows (see <c>RouteableRegionItemsAutomationPeer</c>).</summary>
    protected override global::Avalonia.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => new Automation.RouteableRegionItemsAutomationPeer(this);

    /// <summary>The <see cref="Region"/> property.</summary>
    public static readonly StyledProperty<string?> RegionProperty =
        AvaloniaProperty.Register<RouteableRegionItems, string?>(nameof(Region));
#elif MAUI
    /// <summary>The <see cref="Region"/> property.</summary>
    public static readonly BindableProperty RegionProperty =
        BindableProperty.Create(nameof(Region), typeof(string), typeof(RouteableRegionItems), null,
            propertyChanged: (bindable, _, _) => ((RouteableRegionItems)bindable).Reconnect());

    /// <summary>The space between two views.</summary>
    public static readonly BindableProperty SpacingProperty =
        BindableProperty.Create(nameof(Spacing), typeof(double), typeof(RouteableRegionItems), 8.0);

    /// <summary>The width of a view: a row holds as many as fit, the others go to the next row.</summary>
    public static readonly BindableProperty MinItemWidthProperty =
        BindableProperty.Create(nameof(MinItemWidth), typeof(double), typeof(RouteableRegionItems), 200.0);
#else
    /// <summary>The automation peer: a group named after what the control shows (see <c>RouteableRegionItemsAutomationPeer</c>).</summary>
    protected override Microsoft.UI.Xaml.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => new Automation.RouteableRegionItemsAutomationPeer(this);

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

    /// <summary>Creates the region.</summary>
    public RouteableRegionItems()
    {
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
        Loaded += (_, _) => Connect();
        Unloaded += (_, _) => Disconnect();
    }
#endif

    private readonly RegionConnection _connection = new();

    /// <summary>The name given to <c>AddRegion</c>; <see langword="null"/> shows the views of the default router.</summary>
    public string? Region
    {
#if AVALONIA
        get => GetValue(RegionProperty);
        set => SetValue(RegionProperty, value);
#else
        get => (string?)GetValue(RegionProperty);
        set => SetValue(RegionProperty, value);
#endif
    }

#if MAUI || ROUTEABLE_UNO || WINUI3
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
#endif

    /// <summary>
    /// Where the router is found. By default the provider of the running host (<c>UseRouteables</c>); set it to show the
    /// regions of another one.
    /// </summary>
    public IServiceProvider? Services { get; set; }

#if MAUI
    // The application's provider: a window's scope would give another router, with another history.
    private IServiceProvider? Provider => Services ?? IPlatformApplication.Current?.Services ?? Handler?.MauiContext?.Services;
#else
    private IServiceProvider? Provider => Services ?? ServiceLocator.Services;
#endif

#if AVALONIA
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
        if (change.Property == RegionProperty)
            Reconnect();
    }
#elif MAUI
    // The handler exists while the control is displayed: listen to the router only for that time.
    /// <inheritdoc />
    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        if (Handler is null)
            Disconnect();
        else
            Reconnect();
    }
#endif

    private void Reconnect()
    {
#if !MAUI
        if (!_connection.IsConnected)
            return;
#endif
        Disconnect();
        Connect();
    }

    private void Connect() => _connection.ConnectItems(Provider, Region, Show);

    // A view has one parent: release them when the control is no longer displayed, so that another control can display them.
    private void Disconnect()
    {
        _connection.Disconnect();
        Release();
    }

    // The views leave the control, so another one can take them.
    private void Release()
    {
#if WPF || AVALONIA
        ItemsSource = null;
#elif MAUI
        if (Content is ScrollView { Content: FlexLayout flex })
        {
            foreach (var holder in flex.Children.OfType<ContentView>())
                holder.Content = null;
            flex.Clear();
        }

        Content = null;
#else
        if (Content is ScrollViewer { Content: ItemsRepeater repeater })
            repeater.ItemsSource = null;

        Content = null;
#endif
    }

    private void Show(IReadOnlyList<object> views) => this.Post(() => Apply(views));

    private void Apply(IReadOnlyList<object> views)
    {
        // A late post after the control was disconnected must not show the views again.
        if (!_connection.Active)
            return;

#if WPF
        foreach (var view in views)
            if (view is DependencyObject shown && LogicalTreeHelper.GetParent(shown) is { } holder && !ReferenceEquals(holder, this))
                throw RegionErrors.AlreadyDisplayed(view, holder, Region);

        // A copy: the list belongs to the binding, and the control must not follow a list that is replaced.
        // Release the views first: one that stays is a child of the control until its container is cleared.
        ItemsSource = null;
        ItemsSource = views.ToList();
#elif AVALONIA
        foreach (var view in views)
            if (view is Visual shown && shown.GetVisualParent() is { } holder && !this.IsVisualAncestorOf(shown))
                throw RegionErrors.AlreadyDisplayed(view, holder, Region);

        // A copy: the list belongs to the binding, and the control must not follow a list that is replaced.
        ItemsSource = views.ToList();
#elif MAUI
        // The views leave the old layout before they enter the new one.
        Release();

        var flex = new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap, AlignItems = Microsoft.Maui.Layouts.FlexAlignItems.Start };
        foreach (var view in views.OfType<View>())
            flex.Add(new ContentView { Content = view, Padding = Spacing / 2, WidthRequest = MinItemWidth });

        Content = new ScrollView { Content = flex };
#else
        // The views leave the old layout before they enter the new one.
        Release();

        // A view is its own element: the repeater lays the views out in a wrapping grid, and scrolls when the rows do not fit.
        var repeater = new ItemsRepeater
        {
            Layout = new UniformGridLayout { MinItemWidth = MinItemWidth, MinColumnSpacing = Spacing, MinRowSpacing = Spacing },
            ItemsSource = views.OfType<FrameworkElement>().ToList()
        };
        Content = new ScrollViewer { Content = repeater };
#endif
    }
}

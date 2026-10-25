using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Regions;

namespace Routeables.Maui;

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
public class RouteableRegionItems : ContentView
{
    public static readonly BindableProperty RegionProperty =
        BindableProperty.Create(nameof(Region), typeof(string), typeof(RouteableRegionItems), null,
            propertyChanged: (bindable, _, _) => ((RouteableRegionItems)bindable).Reconnect());

    /// <summary>The space between two views.</summary>
    public static readonly BindableProperty SpacingProperty =
        BindableProperty.Create(nameof(Spacing), typeof(double), typeof(RouteableRegionItems), 8.0);

    /// <summary>The width of a view: a row holds as many as fit, the others go to the next row.</summary>
    public static readonly BindableProperty MinItemWidthProperty =
        BindableProperty.Create(nameof(MinItemWidth), typeof(double), typeof(RouteableRegionItems), 200.0);

    private RegionItemsBinding? _binding;
    private bool _active;

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

    /// <summary>The width of a view: a row holds as many as fit, the others go to the next row.</summary>
    public double MinItemWidth
    {
        get => (double)GetValue(MinItemWidthProperty);
        set => SetValue(MinItemWidthProperty, value);
    }

    /// <summary>Where the router is found. By default the services of the app the control is displayed in.</summary>
    public IServiceProvider? Services { get; set; }

    // The handler exists while the control is displayed: the router is listened to for that time only.
    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        if (Handler is null)
            Disconnect();
        else
            Reconnect();
    }

    private void Reconnect()
    {
        Disconnect();

        // The application's provider, as for RouteableRegion: a window's scope would give another router, with another history.
        var provider = Services ?? IPlatformApplication.Current?.Services ?? Handler?.MauiContext?.Services;
        if (provider is null) return;

        _active = true;
        _binding = new RegionItemsBinding(provider.GetRouter(Region), Show);
    }

    // A view has one parent: they are released when the control is no longer displayed, so another control can display them.
    private void Disconnect()
    {
        _active = false;
        _binding?.Dispose();
        _binding = null;
        Release();
    }

    private void Release()
    {
        if (Content is ScrollView { Content: FlexLayout flex })
        {
            foreach (var holder in flex.Children.OfType<ContentView>())
                holder.Content = null;
            flex.Clear();
        }

        Content = null;
    }

    private void Show(IReadOnlyList<object> views)
    {
        if (Dispatcher.IsDispatchRequired)
            Dispatcher.Dispatch(() => Apply(views));
        else
            Apply(views);
    }

    private void Apply(IReadOnlyList<object> views)
    {
        if (!_active) return;

        // The views leave the old layout before they enter the new one.
        Release();

        var flex = new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap, AlignItems = Microsoft.Maui.Layouts.FlexAlignItems.Start };
        foreach (var view in views.OfType<View>())
            flex.Add(new ContentView { Content = view, Padding = Spacing / 2, WidthRequest = MinItemWidth });

        Content = new ScrollView { Content = flex };
    }
}

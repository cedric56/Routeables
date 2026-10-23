using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Regions;

namespace Routeables.Maui;

/// <summary>
/// The zone of a view that receives a region: it shows the current view of the router registered under <see cref="Region"/>
/// (<c>services.AddRegion("detail")</c>), and follows it when that router routes.
/// <para>
/// Nobody assigns the view to this control. Whoever routes the region (a view model, a menu entry whose
/// <see cref="IRouteableEntry.Region"/> names it, <c>provider.GoToAsync(entry)</c>) only names the region; the control with the
/// same name displays the result. A view can hold several of them, and without <see cref="Region"/> the control shows the
/// default router.
/// </para>
/// <code>
/// &lt;routeables:RouteableRegion Region="detail" /&gt;
/// </code>
/// </summary>
public class RouteableRegion : ContentView
{
    public static readonly BindableProperty RegionProperty =
        BindableProperty.Create(nameof(Region), typeof(string), typeof(RouteableRegion), null,
            propertyChanged: (bindable, _, _) => ((RouteableRegion)bindable).Reconnect());

    private RegionBinding? _binding;
    private bool _active;

    /// <summary>The name given to <c>AddRegion</c>; <see langword="null"/> shows the default router.</summary>
    public string? Region
    {
        get => (string?)GetValue(RegionProperty);
        set => SetValue(RegionProperty, value);
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

        // The routers of the regions are scoped (AddRegion), and the view models that route them are built from the provider of the
        // application. MAUI gives each window a scope of its own: resolving the router there would give the control another router,
        // with another history, than the one that is routed, and the zone would stay empty. The application's provider is the one.
        var provider = Services ?? IPlatformApplication.Current?.Services ?? Handler?.MauiContext?.Services;
        if (provider is null) return;

        // Set before the binding exists: it shows the current view as soon as it is built.
        _active = true;
        _binding = new RegionBinding(provider.GetRouter(Region), Show);
    }

    // A view has one parent: it is released when the control is no longer displayed, so another control can display it.
    private void Disconnect()
    {
        _active = false;
        _binding?.Dispose();
        _binding = null;
        Content = null;
    }

    private void Show(object? view)
    {
        if (Dispatcher.IsDispatchRequired)
            Dispatcher.Dispatch(() => Apply(view));
        else
            Apply(view);
    }

    private void Apply(object? view)
    {
        if (_active)
            Content = view as View;
    }
}
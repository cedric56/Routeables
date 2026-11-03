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
/// &lt;routeables:RouteableRegion Region="detail" Transition="Slide" /&gt;
/// </code>
/// <para>
/// The view can come in with a transition: <see cref="Transition"/> for the region, or
/// <c>RouteableArgs.Create().WithTransition(RouteTransition.Slide)</c> for one navigation, which wins. See <see cref="RouteTransitions"/>.
/// </para>
/// </summary>
public class RouteableRegion : ContentView
{
    /// <summary>The <see cref="Region"/> property.</summary>
    public static readonly BindableProperty RegionProperty =
        BindableProperty.Create(nameof(Region), typeof(string), typeof(RouteableRegion), null,
            propertyChanged: (bindable, _, _) => ((RouteableRegion)bindable).Reconnect());

    /// <summary>The <see cref="TransitionDuration"/> property.</summary>
    public static readonly BindableProperty TransitionDurationProperty =
        BindableProperty.Create(nameof(TransitionDuration), typeof(TimeSpan), typeof(RouteableRegion), TimeSpan.Zero);

    /// <summary>The <see cref="Transition"/> property.</summary>
    public static readonly BindableProperty TransitionProperty =
        BindableProperty.Create(nameof(Transition), typeof(string), typeof(RouteableRegion), null);

    private RegionBinding? _binding;
    private bool _active;

    /// <summary>
    /// The name of the transition that brings in each view of this region (<c>None</c>, <c>Fade</c>, <c>Slide</c>, <c>SlideUp</c> or a name
    /// registered with <see cref="RouteTransitions.Register"/>); a navigation can choose its own. Empty plays none (or the fade of <see cref="TransitionDuration"/>).
    /// </summary>
    public string? Transition
    {
        get => (string?)GetValue(TransitionProperty);
        set => SetValue(TransitionProperty, value);
    }

    /// <summary>The time the new view takes to fade in; zero (the default) shows it at once.</summary>
    public TimeSpan TransitionDuration
    {
        get => (TimeSpan)GetValue(TransitionDurationProperty);
        set => SetValue(TransitionDurationProperty, value);
    }

    /// <summary>The name given to <c>AddRegion</c>; <see langword="null"/> shows the default router.</summary>
    public string? Region
    {
        get => (string?)GetValue(RegionProperty);
        set => SetValue(RegionProperty, value);
    }

    /// <summary>Where the router is found. By default the services of the app the control is displayed in.</summary>
    public IServiceProvider? Services { get; set; }

    // The handler exists while the control is displayed: the router is listened to for that time only.
    /// <inheritdoc />
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

    private void Show(object? view, RegionChange change)
    {
        if (Dispatcher.IsDispatchRequired)
            Dispatcher.Dispatch(() => Apply(view, change));
        else
            Apply(view, change);
    }

    private void Apply(object? view, RegionChange change)
    {
        if (!_active)
            return;

        var shown = view as View;
        Release(shown);
        Content = shown;

        // The transition of the navigation, else the one of the region, else the fade of TransitionDuration.
        if (Content is not null)
            _ = RouteTransitions.PlayAsync(this, change.Resolve(string.IsNullOrWhiteSpace(Transition) ? null : RouteTransition.Named(Transition), TransitionDuration));
    }

    // Another region bound to the same router (the pages of a Shell written in XAML) can still hold the view: a view has one parent,
    // so the last region to show it takes it.
    private void Release(View? view)
    {
        switch (view?.Parent)
        {
            case ContentView other when !ReferenceEquals(other, this):
                other.Content = null;
                break;
            case Layout layout:
                layout.Remove(view);
                break;
        }
    }
}

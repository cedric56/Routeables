using Routeables.Abstraction;
using Routeables.Abstraction.Routing;
using Routeables.Abstraction.Transitions;
using Routeables.Regions;
using System.Windows;
using System.Windows.Controls;

namespace Routeables.Wpf;

/// <summary>
/// Shows the current view of a region: a keyed router with its own history. Put it in a view and set <see cref="Region"/>;
/// route to the region (<c>Region = "name"</c> on the entry) and the view appears here.
/// <para>
/// The view can come in with a transition: <see cref="Transition"/> for the region, or
/// <c>RouteableArgs.Create().WithTransition(RouteTransition.Slide)</c> for one navigation, which wins. See <see cref="RouteTransitions"/>.
/// </para>
/// </summary>
public class RouteableRegion : ContentControl
{
    /// <summary>Identifies <see cref="Route"/>.</summary>
    public static readonly DependencyProperty RouteProperty =
        DependencyProperty.Register(nameof(Route), typeof(string), typeof(RouteableRegion), new PropertyMetadata(null));

    /// <summary>Identifies <see cref="Region"/>.</summary>
    public static readonly DependencyProperty RegionProperty =
        DependencyProperty.Register(nameof(Region), typeof(string), typeof(RouteableRegion),
            new PropertyMetadata(null, (d, _) => ((RouteableRegion)d).Reconnect()));

    /// <summary>Identifies <see cref="TransitionDuration"/>.</summary>
    public static readonly DependencyProperty TransitionDurationProperty =
        DependencyProperty.Register(nameof(TransitionDuration), typeof(TimeSpan), typeof(RouteableRegion), new PropertyMetadata(TimeSpan.Zero));

    /// <summary>Identifies <see cref="Transition"/>.</summary>
    public static readonly DependencyProperty TransitionProperty =
        DependencyProperty.Register(nameof(Transition), typeof(string), typeof(RouteableRegion), new PropertyMetadata(null));

    private readonly RegionConnection _connection = new();

    /// <summary>
    /// The name of the transition that brings in each view of this region (<c>None</c>, <c>Fade</c>, <c>Slide</c>, <c>SlideUp</c> or a name
    /// registered with <see cref="RouteTransitions.Register"/>); a navigation can choose its own. Empty plays none (or the fade of <see cref="TransitionDuration"/>).
    /// </summary>
    public string? Transition
    {
        get => (string?)GetValue(TransitionProperty);
        set => SetValue(TransitionProperty, value);
    }

    /// <summary>
    /// Gets or sets the time the new view takes to fade in; zero (the default) shows it at once.
    /// </summary>
    public TimeSpan TransitionDuration
    {
        get => (TimeSpan)GetValue(TransitionDurationProperty);
        set => SetValue(TransitionDurationProperty, value);
    }

    /// <summary>Creates the region control.</summary>
    public RouteableRegion()
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

    /// <summary>The page this region opens when it appears and its router shows nothing yet, such as <c>/hello/World</c>; it is how an application without a menu says its first page. Without it the default region opens "/" and a named region waits for a page to route it.</summary>
    public string? Route
    {
        get => (string?)GetValue(RouteProperty);
        set => SetValue(RouteProperty, value);
    }

    /// <summary>Gets or sets the container to read the region from; the one of the application when not set.</summary>
    public IServiceProvider? Services { get; set; }

    private void Reconnect()
    {
        if (!_connection.IsConnected) return;

        Disconnect();
        Connect();
    }

    private void Connect()
    {
        var provider = Services ?? ServiceLocator.Services;
        // The binding shows the current view as soon as it is built.
        if (!_connection.Connect(provider, Region, router => new RegionBinding(router, Show)))
            return;

        // The default region starts the routing of an application that has no menu (the binding is there to show the first page).
        RegionStart.Begin(provider!, Region, Route);
    }

    // A view has one parent: it is released when the control is unloaded, so another control can display it.
    private void Disconnect()
    {
        _connection.Disconnect();
        Content = null;
    }

    private void Show(object? view, RegionChange change)
    {
        if (Dispatcher.CheckAccess())
            Apply(view, change);
        else
            Dispatcher.InvokeAsync(() => Apply(view, change));
    }

    private void Apply(object? view, RegionChange change)
    {
        // A late post after the control was disconnected must not show a view again.
        if (!_connection.Active) return;

        // A view has one parent. Say who holds it instead of the bare "Specified element is already the logical child of another element".
        if (view is DependencyObject shown && LogicalTreeHelper.GetParent(shown) is { } holder && !ReferenceEquals(holder, this))
            throw RegionErrors.AlreadyDisplayed(view, holder, Region);

        Content = view;

        // The transition of the navigation, else the one of the region, else the fade of TransitionDuration.
        if (view is not null)
            RouteTransitions.Play(this, change.Resolve(string.IsNullOrWhiteSpace(Transition) ? null : RouteTransition.Named(Transition), TransitionDuration));
    }
}

// Uno is what is left when no other host is named: the Routeables.Uno project links this file without defining a symbol.
#if !WPF && !AVALONIA && !MAUI && !WINUI3
#define ROUTEABLE_UNO
#endif
#if WPF
using System.Windows;
using System.Windows.Controls;
using Routeables.Abstraction.Transitions;
#elif AVALONIA
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Routeables.Abstraction.Transitions;
#elif MAUI
using Routeables.Abstraction.Transitions;
#else
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
#endif
using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Routing;
using Routeables.Regions;

// One file for every host: Routeables.Wpf, Routeables.Avalonia and Routeables.Maui link it (and define WPF, AVALONIA or MAUI),
// Routeables.Uno and Routeables.WinUI3 link it too. What the hosts do the same way is written once; what each does with its own
// controls (the properties, the way a view comes in) is in a branch of its own.
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
/// Shows the current view of a region: a keyed router with its own history. Put it in a view and set <see cref="Region"/>;
/// route to the region (<c>Region = "name"</c> on the entry) and the view appears here.
/// <para>
/// The view can come in with a transition: <see cref="Transition"/> for the region, or
/// <c>RouteableArgs.Create().WithTransition(RouteTransition.Slide)</c> for one navigation, which wins. See <see cref="RouteTransitions"/>.
/// </para>
/// </summary>
public partial class RouteableRegion : ContentControl
#elif AVALONIA
/// <summary>
/// The zone of a view that receives a region: it shows the current view of the router registered under <see cref="Region"/>
/// (<c>services.AddRegion("detail")</c>), and follows it when that router routes.
/// <para>
/// Nobody assigns the view to this control. Whoever routes the region (a view model, a menu entry whose
/// <see cref="IRouteableMenuEntry.Region"/> names it, <c>provider.GoToAsync(entry)</c>) only names the region; the control with the
/// same name displays the result. A view can hold several of them, and without <see cref="Region"/> the control shows the
/// default router.
/// </para>
/// <code>
/// &lt;routeables:RouteableRegion Region="detail"&gt;
///     &lt;routeables:RouteableRegion.PageTransition&gt;&lt;CrossFade Duration="0:0:0.2" /&gt;&lt;/routeables:RouteableRegion.PageTransition&gt;
/// &lt;/routeables:RouteableRegion&gt;
/// </code>
/// <para>
/// A transition can be chosen for the region (<c>Transition="Slide"</c>) or for one navigation
/// (<c>RouteableArgs.Create().WithTransition(RouteTransition.Fade)</c>, which wins): see <see cref="RouteTransitions"/>. Without
/// either, the <c>PageTransition</c> set on the control plays as before.
/// </para>
/// </summary>
public partial class RouteableRegion : TransitioningContentControl
#elif MAUI
/// <summary>
/// The zone of a view that receives a region: it shows the current view of the router registered under <see cref="Region"/>
/// (<c>services.AddRegion("detail")</c>), and follows it when that router routes.
/// <para>
/// Nobody assigns the view to this control. Whoever routes the region (a view model, a menu entry whose
/// <see cref="IRouteableMenuEntry.Region"/> names it, <c>provider.GoToAsync(entry)</c>) only names the region; the control with the
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
public partial class RouteableRegion : ContentView
#else
/// <summary>
/// The zone of a view that receives a region: it shows the current view of the router registered under <see cref="Region"/>
/// (<c>services.AddRegion("detail")</c>), and follows it when that router routes.
/// <para>
/// Nobody assigns the view to this control. Whoever routes the region (a view model, a menu entry whose
/// <see cref="IRouteableMenuEntry.Region"/> names it, <c>provider.GoToAsync(entry)</c>) only names the region; the control with the
/// same name displays the result. A view can hold several of them, and without <see cref="Region"/> the control shows the
/// default router.
/// </para>
/// <code>
/// &lt;routeables:RouteableRegion Region="detail" Transition="Slide" /&gt;
/// </code>
/// <para>
/// The view can come in with a transition: <see cref="RouteTransition"/> for the region, or
/// <c>RouteableArgs.Create().WithTransition(RouteTransition.Slide)</c> for one navigation, which wins. See <see cref="RouteTransitions"/>.
/// </para>
/// </summary>
public partial class RouteableRegion : ContentControl
#endif
{
#if WPF
    /// <summary>The automation peer: a group named after what the control shows (see <c>RouteableRegionAutomationPeer</c>).</summary>
    protected override System.Windows.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => new Automation.RouteableRegionAutomationPeer(this);

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

    /// <summary>Creates the region control.</summary>
    public RouteableRegion()
    {
        Loaded += (_, _) => Connect();
        Unloaded += (_, _) => Disconnect();
    }
#elif AVALONIA
    /// <summary>The automation peer: a group named after what the control shows (see <c>RouteableRegionAutomationPeer</c>).</summary>
    protected override global::Avalonia.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => new Automation.RouteableRegionAutomationPeer(this);

    /// <summary>The <see cref="Region"/> property.</summary>
    public static readonly StyledProperty<string?> RegionProperty =
        AvaloniaProperty.Register<RouteableRegion, string?>(nameof(Region));

    /// <summary>The <see cref="Route"/> property.</summary>
    public static readonly StyledProperty<string?> RouteProperty =
        AvaloniaProperty.Register<RouteableRegion, string?>(nameof(Route));

    /// <summary>The <see cref="Transition"/> property.</summary>
    public static readonly StyledProperty<string?> TransitionProperty =
        AvaloniaProperty.Register<RouteableRegion, string?>(nameof(Transition));

    private IPageTransition? _userTransition;
    private bool _overridden;
#elif MAUI
    /// <summary>The <see cref="Route"/> property.</summary>
    public static readonly BindableProperty RouteProperty =
        BindableProperty.Create(nameof(Route), typeof(string), typeof(RouteableRegion), null);

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
#else
    /// <summary>The automation peer: a group named after what the control shows (see <c>RouteableRegionAutomationPeer</c>).</summary>
    protected override Microsoft.UI.Xaml.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => new Automation.RouteableRegionAutomationPeer(this);

    /// <summary>The page the region opens when it appears: see <see cref="Route"/>.</summary>
    public static readonly DependencyProperty RouteProperty =
        DependencyProperty.Register(nameof(Route), typeof(string), typeof(RouteableRegion), new PropertyMetadata(null));

    /// <summary>The name given to <c>AddRegion</c>; empty or <see langword="null"/> shows the default router.</summary>
    public static readonly DependencyProperty RegionProperty =
        DependencyProperty.Register(nameof(Region), typeof(string), typeof(RouteableRegion),
            new PropertyMetadata(null, (d, _) => ((RouteableRegion)d).Reconnect()));

    /// <summary>Identifies <see cref="RouteTransitionDuration"/>.</summary>
    public static readonly DependencyProperty RouteTransitionDurationProperty =
        DependencyProperty.Register(nameof(RouteTransitionDuration), typeof(TimeSpan), typeof(RouteableRegion),
            new PropertyMetadata(TimeSpan.Zero));

    /// <summary>Identifies <see cref="RouteTransition"/>.</summary>
    public static readonly DependencyProperty RouteTransitionProperty =
        DependencyProperty.Register(nameof(RouteTransition), typeof(string), typeof(RouteableRegion), new PropertyMetadata(null));

    /// <summary>Creates the region.</summary>
    public RouteableRegion()
    {
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
        Loaded += (_, _) => Connect();
        Unloaded += (_, _) => Disconnect();
    }
#endif

    private readonly RegionConnection _connection = new();

#if ROUTEABLE_UNO || WINUI3
    /// <summary>
    /// Gets or sets whether the new view comes in with the theme transition of the platform: any value above zero turns it on, zero (the
    /// default) shows the view at once. The platform chooses the length of the animation.
    /// </summary>
    public TimeSpan RouteTransitionDuration
    {
        get => (TimeSpan)GetValue(RouteTransitionDurationProperty);
        set => SetValue(RouteTransitionDurationProperty, value);
    }

    /// <summary>
    /// The name of the transition that brings in each view of this region (<c>None</c>, <c>Fade</c>, <c>Slide</c>, <c>SlideUp</c> or a name
    /// registered with <see cref="RouteTransitions.Register"/>); a navigation can choose its own. Empty plays none (or the fade of <see cref="RouteTransitionDuration"/>).
    /// </summary>
    public string? RouteTransition
    {
        get => (string?)GetValue(RouteTransitionProperty);
        set => SetValue(RouteTransitionProperty, value);
    }
#else
    /// <summary>
    /// The name of the transition that brings in each view of this region (<c>None</c>, <c>Fade</c>, <c>Slide</c>, <c>SlideUp</c> or a name
    /// registered with <see cref="RouteTransitions.Register"/>); a navigation can choose its own. Empty plays none (or the fade of the duration), or keeps <c>PageTransition</c> (Avalonia).
    /// </summary>
    public string? Transition
    {
#if AVALONIA
        get => GetValue(TransitionProperty);
#else
        get => (string?)GetValue(TransitionProperty);
#endif
        set => SetValue(TransitionProperty, value);
    }
#endif

#if WPF || MAUI
    /// <summary>The time the new view takes to fade in; zero (the default) shows it at once.</summary>
    public TimeSpan TransitionDuration
    {
        get => (TimeSpan)GetValue(TransitionDurationProperty);
        set => SetValue(TransitionDurationProperty, value);
    }
#endif

    /// <summary>The name given to <c>AddRegion</c>; <see langword="null"/> shows the default router; empty is the default one.</summary>
    public string? Region
    {
#if AVALONIA
        get => GetValue(RegionProperty);
#else
        get => (string?)GetValue(RegionProperty);
#endif
        set => SetValue(RegionProperty, value);
    }

    /// <summary>The page this region opens when it appears and its router shows nothing yet, such as <c>/hello/World</c>; it is how an application without a menu says its first page. Without it the default region opens "/" and a named region waits for a page to route it.</summary>
    public string? Route
    {
#if AVALONIA
        get => GetValue(RouteProperty);
#else
        get => (string?)GetValue(RouteProperty);
#endif
        set => SetValue(RouteProperty, value);
    }

    /// <summary>
    /// Where the router is found. By default the provider of the running host (<c>UseRouteables</c>); set it to show the
    /// regions of another one.
    /// </summary>
    public IServiceProvider? Services { get; set; }

#if MAUI
    // The routers of the regions are scoped (AddRegion), and the view models that route them are built from the provider of the
    // application. MAUI gives each window a scope of its own: resolving the router there would give the control another router,
    // with another history, than the one that is routed, and the zone would stay empty. The application's provider is the one.
    private IServiceProvider? Provider => Services ?? IPlatformApplication.Current?.Services ?? Handler?.MauiContext?.Services;

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
#else
    private IServiceProvider? Provider => Services ?? ServiceLocator.Services;
#endif

#if AVALONIA
    // The control keeps the style of TransitioningContentControl.
    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(TransitioningContentControl);

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
#endif

    private void Reconnect()
    {
#if MAUI
        Disconnect();
#else
        if (!_connection.IsConnected)
            return;

        Disconnect();
#endif
        Connect();
    }

    // Shows the current view as soon as it is built, then starts the routing of an application that has no menu.
    private void Connect() => _connection.ConnectCurrent(Provider, Region, Route, Show);

    // A view has one parent: it is released when the control is no longer displayed, so another control can display it.
    private void Disconnect()
    {
        _connection.Disconnect();
        Content = null;
    }

    // The call can come from another thread: Apply then runs later, on the UI thread, and its failure reaches nobody who could take the view for not shown.
    private void Show(object? view, RegionChange change) => this.Post(() =>
    {
        try
        {
            Apply(view, change);
        }
        catch
        {
            _connection.ShowFailed(view);
            throw;
        }
    });

    private void Apply(object? view, RegionChange change)
    {
        // A late post after the control was disconnected must not show a view again.
        if (!_connection.Active)
            return;

#if WPF
        // A view has one parent. Say who holds it instead of the bare "Specified element is already the logical child of another element".
        if (view is DependencyObject shown && LogicalTreeHelper.GetParent(shown) is { } holder && !ReferenceEquals(holder, this))
            throw RegionErrors.AlreadyDisplayed(view, holder, Region);

        Content = view;

        // The transition of the navigation, else the one of the region, else the fade of TransitionDuration.
        if (view is not null)
            RouteTransitions.Play(this, change.Resolve(string.IsNullOrWhiteSpace(Transition) ? null : RouteTransition.Named(Transition), TransitionDuration));
#elif AVALONIA
        // A view has one parent. A host that has left the screen can still hold the view of the router (its transition never ends, so
        // its presenter keeps the page): that one is released. Any other holder is a real duplicate: say who it is instead of the
        // bare "already has a visual parent" of the framework.
        if (view is Visual shown && shown.GetVisualParent() is { } holder)
        {
            if (this.IsVisualAncestorOf(shown))
                ReleaseFromTransition(holder, view);
            else if (!ReleaseFromLeftHost(holder))
                throw RegionErrors.AlreadyDisplayed(view, holder, Region);
        }

        var animate = ApplyTransition(change, out var transition);
        Content = view;
        if (animate && view is Visual visual)
            RouteTransitions.AnimateIn(visual, transition, Bounds.Size);
#elif MAUI
        var shown = view as View;
        Release(shown);
        Content = shown;

        // The transition of the navigation, else the one of the region, else the fade of TransitionDuration.
        if (Content is not null)
            _ = RouteTransitions.PlayAsync(this, change.Resolve(string.IsNullOrWhiteSpace(Transition) ? null : RouteTransition.Named(Transition), TransitionDuration));
#else
        // A view has one parent. Say who holds it instead of the bare error of the framework.
        if (view is DependencyObject shown && VisualTreeHelper.GetParent(shown) is { } holder && !IsAncestorOf(shown))
            throw RegionErrors.AlreadyDisplayed(view, holder, Region);

        // The transition of the navigation, else the one of the region, else the theme transition of RouteTransitionDuration: set before the
        // content changes, which is what plays it.
        ContentTransitions = RouteTransitions.Create(
            change.Resolve(string.IsNullOrWhiteSpace(RouteTransition) ? null : Routeables.Abstraction.Transitions.RouteTransition.Named(RouteTransition), RouteTransitionDuration), ActualWidth, ActualHeight);
        Content = view;
#endif
    }

#if AVALONIA
    // The transition of the navigation, else the one of the region, else the PageTransition set on the control.
    // A custom one is an IPageTransition for the control; a built-in one is played on the view (see RouteTransitions.AnimateIn), and the
    // control then swaps the content at once. Returns whether a built-in transition has to be played on the view.
    private bool ApplyTransition(RegionChange change, out RegionTransition transition)
    {
        var declared = string.IsNullOrWhiteSpace(Transition) ? null : RouteTransition.Named(Transition);
        transition = change.Resolve(declared, TimeSpan.Zero);
        if (!transition.Declared)
        {
            if (_overridden)
            {
                PageTransition = _userTransition;
                IsTransitionReversed = false;
                _overridden = false;
            }

            return false;
        }

        if (!_overridden)
        {
            _userTransition = PageTransition;
            _overridden = true;
        }

        var custom = RouteTransitions.CreateCustom(transition);
        if (!ReferenceEquals(PageTransition, custom))
            PageTransition = custom;
        var reversed = custom is not null && transition.Direction == RouteableDirection.Back;
        if (IsTransitionReversed != reversed)
            IsTransitionReversed = reversed;

        return custom is null && !transition.IsNone;
    }

    // A page that is shown again while the transition that took it out is still playing (Back right after a push, a quick Forward): the control
    // keeps the page it is leaving in its second presenter until the animation ends, and cannot add it to the first one at the same time.
    // That page is going to be the current one again, so the second presenter lets it go. The binding of the first presenter is not touched
    // (SetCurrentValue), and the transition goes on over an empty presenter.
    private void ReleaseFromTransition(Visual holder, object view)
    {
        if (!ReferenceEquals(Content, view) && holder is ContentPresenter presenter)
            presenter.SetCurrentValue(ContentPresenter.ContentProperty, null);
    }

    // The holder is the presenter of a RouteableRegion that is no longer connected (it left the visual tree): it is not showing the
    // view any more, so the view is taken back from it. A host that is still connected keeps its view.
    private static bool ReleaseFromLeftHost(Visual holder)
    {
        var other = holder as RouteableRegion ?? holder.FindAncestorOfType<RouteableRegion>();
        if (other is null || other._connection.Active || holder is not ContentPresenter presenter)
            return false;

        presenter.Content = null;
        return true;
    }
#elif MAUI
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
#elif !WPF
    private bool IsAncestorOf(DependencyObject element)
    {
        for (var current = VisualTreeHelper.GetParent(element); current is not null; current = VisualTreeHelper.GetParent(current))
            if (ReferenceEquals(current, this))
                return true;

        return false;
    }
#endif
}

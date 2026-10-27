using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Regions;

namespace Routeables.Avalonia;

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
public class RouteableRegion : TransitioningContentControl
{
    /// <summary>The <see cref="Region"/> property.</summary>
    public static readonly StyledProperty<string?> RegionProperty =
        AvaloniaProperty.Register<RouteableRegion, string?>(nameof(Region));

    /// <summary>The <see cref="Transition"/> property.</summary>
    public static readonly StyledProperty<string?> TransitionProperty =
        AvaloniaProperty.Register<RouteableRegion, string?>(nameof(Transition));

    private RegionBinding? _binding;
    private bool _active;
    private IPageTransition? _userTransition;
    private bool _overridden;

    /// <summary>
    /// The name of the transition that brings in each view of this region (<c>None</c>, <c>Fade</c>, <c>Slide</c>, <c>SlideUp</c> or a name
    /// registered with <see cref="RouteTransitions.Register"/>); a navigation can choose its own. Empty keeps <c>PageTransition</c>.
    /// </summary>
    public string? Transition
    {
        get => GetValue(TransitionProperty);
        set => SetValue(TransitionProperty, value);
    }

    /// <summary>The name given to <c>AddRegion</c>; <see langword="null"/> shows the default router.</summary>
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

        // Set before the binding exists: it shows the current view as soon as it is built.
        _active = true;
        _binding = new RegionBinding(provider.GetRouter(Region), Show);
    }

    // A view has one parent: it is released when the control leaves the tree, so another control can display it.
    private void Disconnect()
    {
        _active = false;
        _binding?.Dispose();
        _binding = null;
        Content = null;
    }

    private void Show(object? view, RegionChange change)
    {
        if (Dispatcher.UIThread.CheckAccess())
            Apply(view, change);
        else
            Dispatcher.UIThread.Post(() => Apply(view, change));
    }

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
        var reversed = custom is not null && transition.Direction == NavigationDirection.Back;
        if (IsTransitionReversed != reversed)
            IsTransitionReversed = reversed;

        return custom is null && !transition.IsNone;
    }

    private void Apply(object? view, RegionChange change)
    {
        // A late post after the control was disconnected must not show a view again.
        if (!_active) return;

        // A view has one parent. A host that has left the screen can still hold the view of the router (its transition never ends, so
        // its presenter keeps the page): that one is released. Any other holder is a real duplicate: say who it is instead of the
        // bare "already has a visual parent" of the framework.
        if (view is Visual shown && shown.GetVisualParent() is { } holder && !this.IsVisualAncestorOf(shown) && !ReleaseFromLeftHost(holder))
            throw new InvalidOperationException(
                $"{view.GetType().Name} is already displayed by {holder.GetType().Name}, so region '{Region ?? "(default)"}' cannot show it too. " +
                "A page shown by a region must not also host its own current view: let the region display it.");

        var animate = ApplyTransition(change, out var transition);
        Content = view;
        if (animate && view is Visual visual)
            RouteTransitions.AnimateIn(visual, transition, Bounds.Size);
    }

    // The holder is the presenter of a RouteableRegion that is no longer connected (it left the visual tree): it is not showing the
    // view any more, so the view is taken back from it. A host that is still connected keeps its view.
    private static bool ReleaseFromLeftHost(Visual holder)
    {
        var other = holder as RouteableRegion ?? holder.FindAncestorOfType<RouteableRegion>();
        if (other is null || other._active || holder is not ContentPresenter presenter)
            return false;

        presenter.Content = null;
        return true;
    }
}

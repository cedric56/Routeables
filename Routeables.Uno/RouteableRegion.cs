using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Routeables.Abstraction;
using Routeables.Regions;

namespace Routeables.Uno;

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
public partial class RouteableRegion : ContentControl
{
    /// <summary>The name given to <c>AddRegion</c>; empty or <see langword="null"/> shows the default router.</summary>
    public static readonly DependencyProperty RegionProperty =
        DependencyProperty.Register(nameof(Region), typeof(string), typeof(RouteableRegion),
            new PropertyMetadata(null, (d, _) => ((RouteableRegion)d).Reconnect()));

    /// <summary>Identifies <see cref="TransitionDuration"/>.</summary>
    public static readonly DependencyProperty TransitionDurationProperty =
        DependencyProperty.Register(nameof(TransitionDuration), typeof(TimeSpan), typeof(RouteableRegion),
            new PropertyMetadata(TimeSpan.Zero, (d, _) => ((RouteableRegion)d).UpdateTransitions()));

    private RegionBinding? _binding;
    private bool _active;

    /// <summary>
    /// Gets or sets whether the new view comes in with the theme transition of the platform: any value above zero turns it on, zero (the
    /// default) shows the view at once. The platform chooses the length of the animation.
    /// </summary>
    public TimeSpan TransitionDuration
    {
        get => (TimeSpan)GetValue(TransitionDurationProperty);
        set => SetValue(TransitionDurationProperty, value);
    }

    private void UpdateTransitions()
        => ContentTransitions = TransitionDuration > TimeSpan.Zero ? new TransitionCollection { new ContentThemeTransition() } : null;

    /// <summary>Creates the region.</summary>
    public RouteableRegion()
    {
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
        Loaded += (_, _) => Connect();
        Unloaded += (_, _) => Disconnect();
    }

    /// <summary>The name given to <c>AddRegion</c>; <see langword="null"/> shows the default router.</summary>
    public string? Region
    {
        get => (string?)GetValue(RegionProperty);
        set => SetValue(RegionProperty, value);
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

        // Set before the binding exists: it shows the current view as soon as it is built.
        _active = true;
        _binding = new RegionBinding(provider.GetRouter(name), Show);
    }

    // A view has one parent: it is released when the control leaves the tree, so another control can display it.
    private void Disconnect()
    {
        _active = false;
        _binding?.Dispose();
        _binding = null;
        Content = null;
    }

    private void Show(object? view)
    {
        if (DispatcherQueue.HasThreadAccess)
            Apply(view);
        else
            DispatcherQueue.TryEnqueue(() => Apply(view));
    }

    private void Apply(object? view)
    {
        // A late post after the control was disconnected must not show a view again.
        if (!_active) return;

        // A view has one parent. Say who holds it instead of the bare error of the framework.
        if (view is DependencyObject shown && VisualTreeHelper.GetParent(shown) is { } holder && !IsAncestorOf(shown))
            throw new InvalidOperationException(
                $"{view.GetType().Name} is already displayed by {holder.GetType().Name}, so region '{Region ?? "(default)"}' cannot show it too. " +
                "A page shown by a region must not also host its own current view: let the region display it.");

        Content = view;
    }

    private bool IsAncestorOf(DependencyObject element)
    {
        for (var current = VisualTreeHelper.GetParent(element); current is not null; current = VisualTreeHelper.GetParent(current))
            if (ReferenceEquals(current, this))
                return true;

        return false;
    }
}

using Avalonia;
using Avalonia.Controls;
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
/// </summary>
public class RouteableRegion : TransitioningContentControl
{
    public static readonly StyledProperty<string?> RegionProperty =
        AvaloniaProperty.Register<RouteableRegion, string?>(nameof(Region));

    private RegionBinding? _binding;
    private bool _active;

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
    protected override Type StyleKeyOverride => typeof(TransitioningContentControl);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Connect();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Disconnect();
        base.OnDetachedFromVisualTree(e);
    }

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

    private void Show(object? view)
    {
        if (Dispatcher.UIThread.CheckAccess())
            Apply(view);
        else
            Dispatcher.UIThread.Post(() => Apply(view));
    }

    private void Apply(object? view)
    {
        // A late post after the control was disconnected must not show a view again.
        if (!_active) return;

        // A view has one parent. Say who holds it instead of the bare "already has a visual parent" of the framework.
        if (view is Visual shown && shown.GetVisualParent() is { } holder && !this.IsVisualAncestorOf(shown))
            throw new InvalidOperationException(
                $"{view.GetType().Name} is already displayed by {holder.GetType().Name}, so region '{Region ?? "(default)"}' cannot show it too. " +
                "A page shown by a region must not also host its own current view: let the region display it.");

        Content = view;
    }
}

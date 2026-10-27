using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Routeables.Abstraction;
using Routeables.Regions;

namespace Routeables.Razor;

/// <summary>
/// The zone of a page that receives a region: it renders the current view of the router registered under <see cref="Region"/>
/// (<c>services.AddRegion("detail")</c>), and renders again when that router routes.
/// <para>
/// Nobody assigns the view to this component. Whoever routes the region (a view model, a menu entry whose
/// <see cref="IRouteableEntry.Region"/> names it, <c>provider.GoToAsync(entry)</c>) only names the region; the component with the
/// same name displays the result. A page can hold several of them, and without <see cref="Region"/> the component shows the
/// default router.
/// </para>
/// <code>
/// &lt;RouteableRegion Region="detail" Transition="Slide" /&gt;
/// </code>
/// <para>
/// The view can come in with a transition: <see cref="Transition"/> for the region, or
/// <c>RouteableArgs.Create().WithTransition(RouteTransition.Slide)</c> for one navigation, which wins. The element gets the classes
/// <c>rt-enter rt-{name} rt-{forward|back|none}</c> and the CSS variable <c>--rt-duration</c>: <c>theme.css</c> animates the built-in
/// names, and a custom one is a CSS animation you write for <c>.rt-enter.rt-yourname</c>.
/// </para>
/// </summary>
public class RouteableRegion : ComponentBase, IDisposable
{
    private RegionBinding? _binding;
    private string? _boundTo;
    private object? _view;
    private bool _disposed;
    private RegionChange _change = RegionChange.Unknown;
    private int _version;

    [Inject]
    private IServiceProvider Services { get; set; } = default!;

    /// <summary>The name given to <c>AddRegion</c>; <see langword="null"/> shows the default router.</summary>
    [Parameter]
    public string? Region { get; set; }

    /// <summary>
    /// The name of the transition that brings in each view of this region (<c>None</c>, <c>Fade</c>, <c>Slide</c>, <c>SlideUp</c> or the name of
    /// a CSS animation of yours); a navigation can choose its own. Empty plays none (or the fade of <see cref="TransitionDuration"/>).
    /// </summary>
    [Parameter]
    public RouteTransition? Transition { get; set; }

    /// <summary>The time a transition takes when it does not give its own; zero takes 250 ms.</summary>
    [Parameter]
    public TimeSpan TransitionDuration { get; set; }

    /// <summary>The CSS class of the element that holds the view.</summary>
    [Parameter]
    public string? Class { get; set; }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (_binding is not null && _boundTo == Region) return;

        _binding?.Dispose();
        _boundTo = Region;
        // The first call, made by the constructor, is in the middle of a render: it only stores the view.
        _binding = new RegionBinding(Services.GetRouter(Region), Show);
    }

    private void Show(object? view, RegionChange change)
    {
        _view = view;
        _change = change;
        _version++;
        if (_binding is not null && !_disposed)
            _ = InvokeAsync(StateHasChanged);
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var transition = _view is null ? default : _change.Resolve(string.IsNullOrWhiteSpace(Transition?.Name) ? null : RouteTransition.Named(Transition.Name), TransitionDuration);
        var animated = _view is not null && !transition.IsNone;

        builder.OpenElement(0, "div");
        if (animated)
        {
            // A new key makes a new element for each view shown: the browser plays the CSS animation when it is added.
            builder.SetKey(_version);
            var direction = transition.Direction == NavigationDirection.Back ? "back" : transition.Direction == NavigationDirection.Forward ? "forward" : "none";
            var classes = $"rt-enter rt-{CssName(transition.Transition.Name)} rt-{direction}";
            builder.AddAttribute(1, "class", string.IsNullOrWhiteSpace(Class) ? classes : $"{Class} {classes}");
            builder.AddAttribute(3, "style", $"--rt-duration:{(int)transition.Duration.TotalMilliseconds}ms");
        }
        else if (!string.IsNullOrWhiteSpace(Class))
        {
            builder.AddAttribute(1, "class", Class);
        }

        if (_view is RouteableFor view)
            builder.AddContent(2, view.Render());
        builder.CloseElement();
    }

    // The name of a transition as a CSS class: letters, digits, dash and underscore, in lower case.
    private static string CssName(string name)
        => new(name.ToLowerInvariant().Select(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' ? c : '-').ToArray());

    /// <inheritdoc />
    public void Dispose()
    {
        _disposed = true;
        _binding?.Dispose();
        _binding = null;
    }
}

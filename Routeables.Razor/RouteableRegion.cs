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
/// &lt;RouteableRegion Region="detail" /&gt;
/// </code>
/// </summary>
public class RouteableRegion : ComponentBase, IDisposable
{
    private RegionBinding? _binding;
    private string? _boundTo;
    private object? _view;
    private bool _disposed;

    [Inject]
    private IServiceProvider Services { get; set; } = default!;

    /// <summary>The name given to <c>AddRegion</c>; <see langword="null"/> shows the default router.</summary>
    [Parameter]
    public string? Region { get; set; }

    /// <summary>The CSS class of the element that holds the view.</summary>
    [Parameter]
    public string? Class { get; set; }

    protected override void OnParametersSet()
    {
        if (_binding is not null && _boundTo == Region) return;

        _binding?.Dispose();
        _boundTo = Region;
        // The first call, made by the constructor, is in the middle of a render: it only stores the view.
        _binding = new RegionBinding(Services.GetRouter(Region), Show);
    }

    private void Show(object? view)
    {
        _view = view;
        if (_binding is not null && !_disposed)
            _ = InvokeAsync(StateHasChanged);
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "div");
        if (!string.IsNullOrWhiteSpace(Class))
            builder.AddAttribute(1, "class", Class);
        if (_view is RouteableFor view)
            builder.AddContent(2, view.Render());
        builder.CloseElement();
    }

    public void Dispose()
    {
        _disposed = true;
        _binding?.Dispose();
        _binding = null;
    }
}

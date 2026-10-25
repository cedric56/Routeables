using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Routeables.Abstraction;
using Routeables.Regions;

namespace Routeables.Razor;

/// <summary>
/// Like <see cref="RouteableRegion"/>, but renders <b>every</b> view of the region instead of the current one: the views of the
/// history of the router registered under <see cref="Region"/> (<c>services.AddRegion("adopt")</c>), oldest first, up to the
/// current one, in rows and columns: a row holds as many views of at least <see cref="MinItemWidth"/> pixels as fit, the others go
/// to the row below. Routing the region adds a view, <c>PopAsync</c> and <c>BackAsync</c>
/// remove the last, and the component renders again. Nobody assigns a view to it: whoever routes the region only names it.
/// <code>
/// &lt;RouteableRegionItems Region="adopt" /&gt;
/// </code>
/// </summary>
public class RouteableRegionItems : ComponentBase, IDisposable
{
    private RegionItemsBinding? _binding;
    private string? _boundTo;
    private IReadOnlyList<object> _views = [];
    private bool _disposed;

    [Inject]
    private IServiceProvider Services { get; set; } = default!;

    /// <summary>The name given to <c>AddRegion</c>; <see langword="null"/> shows the views of the default router.</summary>
    [Parameter]
    public string? Region { get; set; }

    /// <summary>The least width of a view, in pixels: a row holds as many as fit, the others go to the next row.</summary>
    [Parameter]
    public int MinItemWidth { get; set; } = 200;

    /// <summary>The CSS class of the element that holds the views (it is a grid; a class can change that).</summary>
    [Parameter]
    public string? Class { get; set; }

    protected override void OnParametersSet()
    {
        if (_binding is not null && _boundTo == Region) return;

        _binding?.Dispose();
        _boundTo = Region;
        // The first call, made by the constructor, is in the middle of a render: it only stores the views.
        _binding = new RegionItemsBinding(Services.GetRouter(Region), Show);
    }

    private void Show(IReadOnlyList<object> views)
    {
        _views = views;
        if (_binding is not null && !_disposed)
            _ = InvokeAsync(StateHasChanged);
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", Class);
        builder.AddAttribute(2, "style", $"display:grid;grid-template-columns:repeat(auto-fill,minmax({MinItemWidth}px,1fr));gap:.5rem;align-items:start");
        foreach (var view in _views.OfType<RouteableFor>())
        {
            builder.OpenElement(3, "div");
            builder.SetKey(view);
            builder.AddAttribute(4, "style", "min-width:0");
            builder.AddContent(5, view.Render());
            builder.CloseElement();
        }

        builder.CloseElement();
    }

    public void Dispose()
    {
        _disposed = true;
        _binding?.Dispose();
        _binding = null;
    }
}

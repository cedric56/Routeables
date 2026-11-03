using Microsoft.Maui.Controls;
using Routeables.Abstraction;

namespace Routeables.Maui;

/// <summary>The <see cref="IRouteableViewBinder"/> of the host: a view that is not a <c>RouteableFor</c> gets its view model as its <c>BindingContext</c>.</summary>
internal sealed class RouteableViewBinder : IRouteableViewBinder
{
    /// <inheritdoc />
    public IRouteableFor? Adapt(object view)
        => view is BindableObject element ? new RouteableContextAccessor(() => element.BindingContext, value => element.BindingContext = value) : null;
}

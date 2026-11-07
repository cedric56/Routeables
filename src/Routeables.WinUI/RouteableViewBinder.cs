using Microsoft.UI.Xaml;
using Routeables.Abstraction;

#if WINUI3
namespace Routeables.WinUI3;
#else
namespace Routeables.Uno;
#endif

/// <summary>The <see cref="IRouteableViewBinder"/> of the host: a view that is not a <c>RouteableFor</c> gets its view model as its <c>DataContext</c>.</summary>
internal sealed class RouteableViewBinder : IRouteableViewBinder
{
    /// <inheritdoc />
    public IRouteableFor? Adapt(object view)
        => view is FrameworkElement element ? new RouteableContextAccessor(() => element.DataContext, value => element.DataContext = value) : null;
}

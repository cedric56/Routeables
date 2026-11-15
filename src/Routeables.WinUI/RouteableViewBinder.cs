#if WPF
using System.Windows;
using ViewElement = System.Windows.FrameworkElement;
#elif AVALONIA
using Avalonia;
using ViewElement = Avalonia.StyledElement;
#elif MAUI
using ViewElement = Microsoft.Maui.Controls.BindableObject;
#else
using Microsoft.UI.Xaml;
using ViewElement = Microsoft.UI.Xaml.FrameworkElement;
#endif
using Routeables.Abstraction;
using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Lifecycle;

// One file for every host: Routeables.Wpf, Routeables.Avalonia and Routeables.Maui link it (and define WPF, AVALONIA or MAUI),
// Routeables.Uno and Routeables.WinUI3 compile this folder.
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

/// <summary>
/// The <see cref="IRouteableViewBinder"/> of the host: a view that is not a <c>RouteableFor</c> gets its view model as its
/// <c>DataContext</c> (its <c>BindingContext</c> in MAUI).
/// </summary>
internal sealed class RouteableViewBinder : IRouteableViewBinder
{
    /// <inheritdoc />
    public IRouteableFor? Adapt(object view)
#if MAUI
        => view is ViewElement element ? new RouteableContextAccessor(() => element.BindingContext, value => element.BindingContext = value) : null;
#else
        => view is ViewElement element ? new RouteableContextAccessor(() => element.DataContext, value => element.DataContext = value) : null;
#endif
}

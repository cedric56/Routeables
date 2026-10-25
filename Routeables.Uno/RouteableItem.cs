using Microsoft.UI.Xaml;

namespace Routeables.Uno;

/// <summary>
/// The route of an item of a control of navigation that the application declares in XAML, for <see cref="RouteableNavigation.AttachRouteables"/>:
/// <c>&lt;NavigationViewItem Content="Cats" routeables:RouteableItem.Route="cats" /&gt;</c> is the route <c>/cats</c>.
/// </summary>
public static class RouteableItem
{
    /// <summary>Identifies the attached property <c>Route</c>.</summary>
    public static readonly DependencyProperty RouteProperty =
        DependencyProperty.RegisterAttached("Route", typeof(string), typeof(RouteableItem), new PropertyMetadata(null));

    /// <summary>Gets the route of <paramref name="item"/>; <see langword="null"/> when it names none.</summary>
    public static string? GetRoute(DependencyObject item) => (string?)item.GetValue(RouteProperty);

    /// <summary>Sets the route of <paramref name="item"/>.</summary>
    public static void SetRoute(DependencyObject item, string? value) => item.SetValue(RouteProperty, value);
}

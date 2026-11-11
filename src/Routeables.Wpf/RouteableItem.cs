using System.Windows;

namespace Routeables.Wpf;

/// <summary>
/// The route of an item of a control of navigation that the application declares in XAML, for <see cref="RouteableTabs.AttachRouteables"/>:
/// <c>&lt;TabItem Header="Cats" routeables:RouteableItem.Route="cats" /&gt;</c> is the route <c>/cats</c>.
/// </summary>
public static class RouteableItem
{
    /// <summary>Identifies the attached property <c>Route</c>.</summary>
    public static readonly DependencyProperty RouteProperty =
        DependencyProperty.RegisterAttached("Route", typeof(string), typeof(RouteableItem), new PropertyMetadata(null));

    /// <summary>
    /// The region of this tab: it then has its own pile of pages, kept when the user goes to another tab and comes back, instead of sharing the region
    /// of the tab control. Register it with <c>AddRegion</c>. The tab goes to its <see cref="RouteProperty"/> the first time it is selected.
    /// </summary>
    public static readonly DependencyProperty RegionProperty =
        DependencyProperty.RegisterAttached("Region", typeof(string), typeof(RouteableItem), new PropertyMetadata(null));

    /// <summary>Gets the region of <paramref name="item"/>; <see langword="null"/> when it shares the region of its control.</summary>
    public static string? GetRegion(DependencyObject item) => (string?)item.GetValue(RegionProperty);

    /// <summary>Sets the region of <paramref name="item"/>.</summary>
    public static void SetRegion(DependencyObject item, string? value) => item.SetValue(RegionProperty, value);

    /// <summary>Gets the route of <paramref name="item"/>; <see langword="null"/> when it names none.</summary>
    public static string? GetRoute(DependencyObject item) => (string?)item.GetValue(RouteProperty);

    /// <summary>Sets the route of <paramref name="item"/>.</summary>
    public static void SetRoute(DependencyObject item, string? value) => item.SetValue(RouteProperty, value);
}

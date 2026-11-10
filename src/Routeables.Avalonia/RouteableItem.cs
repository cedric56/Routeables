using Avalonia;
using Avalonia.Controls;

namespace Routeables.Avalonia;

/// <summary>
/// The route of an item of a control of navigation that the application declares in XAML, for <see cref="RouteableTabs.AttachRouteables"/>:
/// <c>&lt;TabItem Header="Cats" routeables:RouteableItem.Route="cats" /&gt;</c> is the route <c>/cats</c>.
/// </summary>
public sealed class RouteableItem : AvaloniaObject
{
    /// <summary>Identifies the attached property <c>Route</c>.</summary>
    public static readonly AttachedProperty<string?> RouteProperty =
        AvaloniaProperty.RegisterAttached<RouteableItem, Control, string?>("Route");

    /// <summary>
    /// The region of this tab: it then has its own pile of pages, kept when the user goes to another tab and comes back, instead of sharing the region
    /// of the tab control. Register it with <c>AddRegion</c>. The tab goes to its <see cref="RouteProperty"/> the first time it is selected.
    /// </summary>
    public static readonly AttachedProperty<string?> RegionProperty =
        AvaloniaProperty.RegisterAttached<RouteableItem, Control, string?>("Region");

    public static string? GetRegion(Control item) => item.GetValue(RegionProperty);

    public static void SetRegion(Control item, string? value) => item.SetValue(RegionProperty, value);

    private RouteableItem()
    {
    }

    /// <summary>Gets the route of <paramref name="item"/>; <see langword="null"/> when it names none.</summary>
    public static string? GetRoute(Control item) => item.GetValue(RouteProperty);

    /// <summary>Sets the route of <paramref name="item"/>.</summary>
    public static void SetRoute(Control item, string? value) => item.SetValue(RouteProperty, value);
}

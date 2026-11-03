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

    private RouteableItem()
    {
    }

    /// <summary>Gets the route of <paramref name="item"/>; <see langword="null"/> when it names none.</summary>
    public static string? GetRoute(Control item) => item.GetValue(RouteProperty);

    /// <summary>Sets the route of <paramref name="item"/>.</summary>
    public static void SetRoute(Control item, string? value) => item.SetValue(RouteProperty, value);
}

#if WPF
using System.Windows;
using ItemTarget = System.Windows.DependencyObject;
#elif AVALONIA
using Avalonia;
using Avalonia.Controls;
using ItemTarget = Avalonia.Controls.Control;
#else
using Microsoft.UI.Xaml;
using ItemTarget = Microsoft.UI.Xaml.DependencyObject;
#endif

// One file for every host that has tabs: Routeables.Wpf and Routeables.Avalonia link it (and define WPF or AVALONIA), Routeables.Uno and
// Routeables.WinUI3 link it too.
#if WPF
namespace Routeables.Wpf;
#elif AVALONIA
namespace Routeables.Avalonia;
#elif WINUI3
namespace Routeables.WinUI3;
#else
namespace Routeables.Uno;
#endif

#if WPF || AVALONIA
/// <summary>
/// The route of an item of a control of navigation that the application declares in XAML, for
/// <see cref="RouteableTabs.AttachRouteables"/>:
/// <c>&lt;TabItem Header="Cats" routeables:RouteableItem.Route="cats" /&gt;</c> is the route <c>/cats</c>.
/// </summary>
#else
/// <summary>
/// The route of an item of a control of navigation that the application declares in XAML, for
/// <see cref="RouteableNavigation.AttachRouteables"/>:
/// <c>&lt;NavigationViewItem Content="Cats" routeables:RouteableItem.Route="cats" /&gt;</c> is the route <c>/cats</c>.
/// </summary>
#endif
#if AVALONIA
public sealed class RouteableItem : AvaloniaObject
#else
public static class RouteableItem
#endif
{
#if AVALONIA
    private RouteableItem()
    {
    }

    /// <summary>Identifies the attached property <c>Route</c>.</summary>
    public static readonly AttachedProperty<string?> RouteProperty =
        AvaloniaProperty.RegisterAttached<RouteableItem, Control, string?>("Route");
#else
    /// <summary>Identifies the attached property <c>Route</c>.</summary>
    public static readonly DependencyProperty RouteProperty =
        DependencyProperty.RegisterAttached("Route", typeof(string), typeof(RouteableItem), new PropertyMetadata(null));
#endif

    /// <summary>
    /// The region of this item: it then has its own pile of pages, kept when the user goes to another item and comes back, instead of sharing the region
    /// of the navigation view. Register it with <c>AddRegion</c>. The item goes to its <see cref="RouteProperty"/> the first time it is selected.
    /// </summary>
#if AVALONIA
    public static readonly AttachedProperty<string?> RegionProperty =
        AvaloniaProperty.RegisterAttached<RouteableItem, Control, string?>("Region");
#else
    public static readonly DependencyProperty RegionProperty =
        DependencyProperty.RegisterAttached("Region", typeof(string), typeof(RouteableItem), new PropertyMetadata(null));
#endif

    /// <summary>Gets the region of <paramref name="item"/>; <see langword="null"/> when it shares the region of its control.</summary>
    public static string? GetRegion(ItemTarget item) => (string?)item.GetValue(RegionProperty);

    /// <summary>Sets the region of <paramref name="item"/>.</summary>
    public static void SetRegion(ItemTarget item, string? value) => item.SetValue(RegionProperty, value);

    /// <summary>Gets the route of <paramref name="item"/>; <see langword="null"/> when it names none.</summary>
    public static string? GetRoute(ItemTarget item) => (string?)item.GetValue(RouteProperty);

    /// <summary>Sets the route of <paramref name="item"/>.</summary>
    public static void SetRoute(ItemTarget item, string? value) => item.SetValue(RouteProperty, value);
}
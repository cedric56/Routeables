#if WPF
using System.Windows;
using System.Windows.Controls.Primitives;
using LinkTarget = System.Windows.DependencyObject;
using LinkButton = System.Windows.Controls.Primitives.ButtonBase;
using LinkClickArgs = System.Windows.RoutedEventArgs;
#elif MAUI
using LinkTarget = Microsoft.Maui.Controls.BindableObject;
using LinkButton = Microsoft.Maui.Controls.Button;
using LinkClickArgs = System.EventArgs;
#elif AVALONIA
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using LinkTarget = Avalonia.Controls.Button;
using LinkButton = Avalonia.Controls.Button;
using LinkClickArgs = Avalonia.Interactivity.RoutedEventArgs;
#else
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;
using LinkTarget = Microsoft.UI.Xaml.DependencyObject;
using LinkButton = Microsoft.UI.Xaml.Controls.Primitives.ButtonBase;
using LinkClickArgs = Microsoft.UI.Xaml.RoutedEventArgs;
#endif
using Routeables.Abstraction;
using Routeables.Abstraction.Routing;
using Routeables.Services;

// One file for every host that has a button: Routeables.Wpf, Routeables.Maui and Routeables.Avalonia link it (and define WPF, MAUI or AVALONIA),
// Routeables.Uno and Routeables.WinUI3 compile this folder. What differs from a host to another is only how an attached property is
// declared and the name of the click event; the rest, the accessors and what a click does, is written once.
#if WPF
namespace Routeables.Wpf;
#elif MAUI
namespace Routeables.Maui;
#elif AVALONIA
namespace Routeables.Avalonia;
#elif WINUI3
namespace Routeables.WinUI3;
#else
namespace Routeables.Uno;
#endif

/// <summary>
/// Goes to a route when a button is clicked, declared in XAML and with no code in the view model:
/// <c>&lt;Button Content="Hello" routeables:RouteableLink.Route="/hello/World" /&gt;</c>. The route is registered like any other;
/// <c>RouteableLink.Region</c> names the region whose router is used (the default router without it).
/// </summary>
#if AVALONIA
public sealed class RouteableLink : AvaloniaObject
#else
public static class RouteableLink
#endif
{
#if AVALONIA
    static RouteableLink() => RouteProperty.Changed.AddClassHandler<LinkButton>((button, e) => Retarget(button, e.NewValue));

    /// <summary>Identifies the attached property <c>Route</c>: the address to go to, such as <c>/hello/World</c>.</summary>
    public static readonly AttachedProperty<string?> RouteProperty =
        AvaloniaProperty.RegisterAttached<RouteableLink, LinkButton, string?>("Route");

    /// <summary>Identifies the attached property <c>Region</c>: the region whose router is used.</summary>
    public static readonly AttachedProperty<string?> RegionProperty =
        AvaloniaProperty.RegisterAttached<RouteableLink, LinkButton, string?>("Region");
#elif MAUI
    /// <summary>Identifies the attached property <c>Route</c>: the address to go to, such as <c>/hello/World</c>.</summary>
    public static readonly BindableProperty RouteProperty =
        BindableProperty.CreateAttached("Route", typeof(string), typeof(RouteableLink), null,
            propertyChanged: (bindable, _, newValue) => { if (bindable is LinkButton button) Retarget(button, newValue); });

    /// <summary>Identifies the attached property <c>Region</c>: the region whose router is used.</summary>
    public static readonly BindableProperty RegionProperty =
        BindableProperty.CreateAttached("Region", typeof(string), typeof(RouteableLink), null);
#else
    /// <summary>Identifies the attached property <c>Route</c>: the address to go to, such as <c>/hello/World</c>.</summary>
    public static readonly DependencyProperty RouteProperty =
        DependencyProperty.RegisterAttached("Route", typeof(string), typeof(RouteableLink),
            new PropertyMetadata(null, (d, e) => { if (d is LinkButton button) Retarget(button, e.NewValue); }));

    /// <summary>Identifies the attached property <c>Region</c>: the region whose router is used.</summary>
    public static readonly DependencyProperty RegionProperty =
        DependencyProperty.RegisterAttached("Region", typeof(string), typeof(RouteableLink), new PropertyMetadata(null));
#endif

    /// <summary>Gets the address <paramref name="button"/> goes to.</summary>
    public static string? GetRoute(LinkTarget button) => (string?)button.GetValue(RouteProperty);

    /// <summary>Sets the address <paramref name="button"/> goes to.</summary>
    public static void SetRoute(LinkTarget button, string? value) => button.SetValue(RouteProperty, value);

    /// <summary>Gets the region of <paramref name="button"/>.</summary>
    public static string? GetRegion(LinkTarget button) => (string?)button.GetValue(RegionProperty);

    /// <summary>Sets the region of <paramref name="button"/>.</summary>
    public static void SetRegion(LinkTarget button, string? value) => button.SetValue(RegionProperty, value);

    private static void Retarget(LinkButton button, object? route)
    {
#if MAUI
        button.Clicked -= OnClick;                     // once, whatever the number of changes
        if (route is string { Length: > 0 })
            button.Clicked += OnClick;
#else
        button.Click -= OnClick;                       // once, whatever the number of changes
        if (route is string { Length: > 0 })
            button.Click += OnClick;
#endif
    }

    private static async void OnClick(object? sender, LinkClickArgs e)
    {
        if (sender is not LinkButton button || GetRoute(button) is not { Length: > 0 } route) return;
#if MAUI
        var provider = IPlatformApplication.Current?.Services ?? button.Handler?.MauiContext?.Services;
#else
        var provider = ServiceLocator.Services;
#endif
        await RouteableLinkAction.GoAsync(provider, route, GetRegion(button));
    }
}

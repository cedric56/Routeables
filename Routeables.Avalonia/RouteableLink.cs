using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Routeables.Abstraction;

namespace Routeables.Avalonia;

/// <summary>
/// Goes to a route when a button is clicked, declared in XAML and with no code in the view model:
/// <c>&lt;Button Content="Hello" routeables:RouteableLink.Route="/hello/World" /&gt;</c>. The route is registered like any other;
/// <c>RouteableLink.Region</c> names the region whose router is used (the default router without it).
/// </summary>
public sealed class RouteableLink : AvaloniaObject
{
    static RouteableLink() => RouteProperty.Changed.AddClassHandler<Button>(OnRouteChanged);

    /// <summary>Identifies the attached property <c>Route</c>: the address to go to, such as <c>/hello/World</c>.</summary>
    public static readonly AttachedProperty<string?> RouteProperty =
        AvaloniaProperty.RegisterAttached<RouteableLink, Button, string?>("Route");

    /// <summary>Identifies the attached property <c>Region</c>: the region whose router is used.</summary>
    public static readonly AttachedProperty<string?> RegionProperty =
        AvaloniaProperty.RegisterAttached<RouteableLink, Button, string?>("Region");

    /// <summary>Gets the address <paramref name="button"/> goes to.</summary>
    public static string? GetRoute(Button button) => button.GetValue(RouteProperty);

    /// <summary>Sets the address <paramref name="button"/> goes to.</summary>
    public static void SetRoute(Button button, string? value) => button.SetValue(RouteProperty, value);

    /// <summary>Gets the region of <paramref name="button"/>.</summary>
    public static string? GetRegion(Button button) => button.GetValue(RegionProperty);

    /// <summary>Sets the region of <paramref name="button"/>.</summary>
    public static void SetRegion(Button button, string? value) => button.SetValue(RegionProperty, value);

    private static void OnRouteChanged(Button button, AvaloniaPropertyChangedEventArgs e)
    {
        button.Click -= OnClick;                       // once, whatever the number of changes
        if (e.NewValue is string { Length: > 0 })
            button.Click += OnClick;
    }

    private static async void OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { } button || GetRoute(button) is not { Length: > 0 } route) return;
        var provider = ServiceLocator.Services;
        try
        {
            if (provider is null) throw new InvalidOperationException("Routeables is not started: call UseRouteables first.");
            var result = await provider.GoToAsync(route, GetRegion(button));
            if (!result.Success && result.Exception is { } failure)
                RouteableErrors.Report(provider, "RouteableLink", failure);
        }
        catch (Exception ex)
        {
            RouteableErrors.Report(provider, "RouteableLink", ex);
        }
    }
}

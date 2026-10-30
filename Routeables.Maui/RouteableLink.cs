using Routeables.Abstraction;

namespace Routeables.Maui;

/// <summary>
/// Goes to a route when a button is clicked, declared in XAML and with no code in the view model:
/// <c>&lt;Button Text="Hello" routeables:RouteableLink.Route="/hello/World" /&gt;</c>. The route is registered like any other;
/// <c>RouteableLink.Region</c> names the region whose router is used (the default router without it).
/// </summary>
public static class RouteableLink
{
    /// <summary>Identifies the attached property <c>Route</c>: the address to go to, such as <c>/hello/World</c>.</summary>
    public static readonly BindableProperty RouteProperty =
        BindableProperty.CreateAttached("Route", typeof(string), typeof(RouteableLink), null, propertyChanged: OnRouteChanged);

    /// <summary>Identifies the attached property <c>Region</c>: the region whose router is used.</summary>
    public static readonly BindableProperty RegionProperty =
        BindableProperty.CreateAttached("Region", typeof(string), typeof(RouteableLink), null);

    /// <summary>Gets the address <paramref name="button"/> goes to.</summary>
    public static string? GetRoute(BindableObject button) => (string?)button.GetValue(RouteProperty);

    /// <summary>Sets the address <paramref name="button"/> goes to.</summary>
    public static void SetRoute(BindableObject button, string? value) => button.SetValue(RouteProperty, value);

    /// <summary>Gets the region of <paramref name="button"/>.</summary>
    public static string? GetRegion(BindableObject button) => (string?)button.GetValue(RegionProperty);

    /// <summary>Sets the region of <paramref name="button"/>.</summary>
    public static void SetRegion(BindableObject button, string? value) => button.SetValue(RegionProperty, value);

    private static void OnRouteChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        if (bindable is not Button button) return;
        button.Clicked -= OnClicked;                   // once, whatever the number of changes
        if (newValue is string { Length: > 0 })
            button.Clicked += OnClicked;
    }

    private static async void OnClicked(object? sender, EventArgs e)
    {
        if (sender is not Button button || GetRoute(button) is not { Length: > 0 } route) return;
        var provider = IPlatformApplication.Current?.Services ?? button.Handler?.MauiContext?.Services;
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

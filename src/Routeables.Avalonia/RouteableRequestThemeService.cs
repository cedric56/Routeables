using Avalonia;
using Avalonia.Styling;
using Routeables.Services;
using Application = Avalonia.Application;

namespace Routeables.Avalonia;

/// <summary>Applies the requested theme through <see cref="Application.RequestedThemeVariant"/>.</summary>
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public class RouteableRequestThemeService : IRouteableRequestedThemeService
{
    /// <summary>The theme shown now (<see cref="Application.ActualThemeVariant"/>): the one the application set, else the one of the system.</summary>
    public RouteableRequestedTheme Current
        => Application.Current?.ActualThemeVariant == ThemeVariant.Dark ? RouteableRequestedTheme.Dark : RouteableRequestedTheme.Light;

    /// <summary>
    /// Applies the specified theme to the application by setting <see cref="Application.RequestedThemeVariant"/>.
    /// </summary>
    /// <param name="theme">The requested theme.</param>
    public void Request(RouteableRequestedTheme theme)
    {
        Application.Current?.RequestedThemeVariant = theme switch
        {
            RouteableRequestedTheme.Light => ThemeVariant.Light,
            RouteableRequestedTheme.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };
    }
}

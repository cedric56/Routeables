using Avalonia;
using Avalonia.Styling;
using Routeables.Services;

namespace Routeables.Avalonia;

/// <summary>Applies the requested theme through <see cref="Application.RequestedThemeVariant"/>.</summary>
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public class RouteableRequestThemeService : IRouteableRequestedThemeService
{
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

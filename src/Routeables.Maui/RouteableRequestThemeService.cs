using Routeables.Services;

namespace Routeables.Maui;

internal class RouteableRequestThemeService : IRouteableRequestedThemeService
{
    /// <summary>The theme shown now (<see cref="Application.RequestedTheme"/>: the <c>UserAppTheme</c> of the application when it set one, else the system's).</summary>
    public RouteableRequestedTheme Current => Application.Current?.RequestedTheme switch
    {
        AppTheme.Dark => RouteableRequestedTheme.Dark,
        AppTheme.Light => RouteableRequestedTheme.Light,
        _ => RouteableRequestedTheme.Default,
    };

    /// <summary>
    /// Applies the specified theme to the application by setting <see cref="Application.UserAppTheme"/>.
    /// </summary>
    /// <param name="theme">The requested theme.</param>
    public void Request(RouteableRequestedTheme theme)
    {
        Application.Current?.UserAppTheme
            = theme switch
            {
                RouteableRequestedTheme.Light => AppTheme.Light,
                RouteableRequestedTheme.Dark => AppTheme.Dark,
                _ => AppTheme.Unspecified,
            };
    }
}

using Routeables.Abstraction;
using Routeables.Services;

namespace Routeables.Maui;

internal class RouteableRequestThemeService : IRouteableRequestedThemeService
{
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

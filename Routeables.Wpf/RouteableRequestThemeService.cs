using System.Windows;
using Routeables.Services;

namespace Routeables.Wpf;

/// <summary>
/// Applies the theme requested by a view model with the Fluent theme of WPF (<c>Application.ThemeMode</c>), the theme of
/// Windows 11 that follows the light or dark setting of the system. WPF marks <c>ThemeMode</c> as experimental (WPF0001).
/// </summary>
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public class RouteableRequestThemeService : IRouteableRequestedThemeService
{
    public void Request(RouteableRequestedTheme theme)
    {
        UiThread.Post(() =>
        {
#pragma warning disable WPF0001 // ThemeMode is an evaluation API of WPF
            if (Application.Current is { } application)
            {
                application.ThemeMode = theme switch
                {
                    RouteableRequestedTheme.Light => ThemeMode.Light,
                    RouteableRequestedTheme.Dark => ThemeMode.Dark,
                    _ => ThemeMode.System,
                };
            }
#pragma warning restore WPF0001
        });
    }
}

using Routeables.Services;
using System.Windows;

namespace Routeables.Wpf;

/// <summary>
/// Applies the theme requested by a view model with the Fluent theme of WPF (<c>Application.ThemeMode</c>), the theme of
/// Windows 11 that follows the light or dark setting of the system. WPF marks <c>ThemeMode</c> as experimental (WPF0001).
/// </summary>
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public class RouteableRequestThemeService : IRouteableRequestedThemeService
{
    /// <summary>The theme shown now: the one the application set with <c>ThemeMode</c>, else the one of Windows (<c>AppsUseLightTheme</c>).</summary>
    public RouteableRequestedTheme Current
    {
        get
        {
#pragma warning disable WPF0001 // ThemeMode is an evaluation API of WPF
            var mode = Application.Current?.ThemeMode;
            if (mode == ThemeMode.Dark)
                return RouteableRequestedTheme.Dark;
            if (mode == ThemeMode.Light)
                return RouteableRequestedTheme.Light;
#pragma warning restore WPF0001
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                return key?.GetValue("AppsUseLightTheme") is int light && light == 0 ? RouteableRequestedTheme.Dark : RouteableRequestedTheme.Light;
            }
            catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or System.IO.IOException)
            {
                return RouteableRequestedTheme.Default;
            }
        }
    }

    /// <inheritdoc />
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
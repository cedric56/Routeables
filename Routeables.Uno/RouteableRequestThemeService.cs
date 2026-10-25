using Microsoft.UI.Xaml;
using Routeables.Services;

namespace Routeables.Uno;

/// <summary>
/// Applies a theme by setting <c>FrameworkElement.RequestedTheme</c> on the content of the active window.
/// <c>Microsoft.UI.Xaml.Application.RequestedTheme</c> cannot change while the application runs, so the theme is carried by the root element.
/// </summary>
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public class RouteableRequestThemeService : IRouteableRequestedThemeService
{
    /// <summary>Applies the specified theme; <see cref="RouteableRequestedTheme.Default"/> follows the system.</summary>
    /// <param name="theme">The requested theme.</param>
    public void Request(RouteableRequestedTheme theme)
    {
        var root = RouteableWindows.ActiveRoot;
        if (root is null)
            return;

        var requested = theme switch
        {
            RouteableRequestedTheme.Light => ElementTheme.Light,
            RouteableRequestedTheme.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };

        if (root.DispatcherQueue is { } queue && !queue.HasThreadAccess)
            queue.TryEnqueue(() => root.RequestedTheme = requested);
        else
            root.RequestedTheme = requested;
    }
}

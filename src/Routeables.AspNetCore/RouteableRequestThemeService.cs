using Routeables.Services;

namespace Routeables.AspNetCore;

/// <summary>
/// The requested theme of a visitor, kept on the server. A view model asks for a theme through <see cref="IRouteableRequestedThemeService"/>
/// and the page that is sent next reads <see cref="Requested"/> to draw itself in it (a <c>data-theme</c> attribute, a class...).
/// The service is scoped: with a session per visitor, each visitor has the theme they asked for.
/// </summary>
public sealed class RouteableRequestThemeService : IRouteableRequestedThemeService
{
    /// <summary>The theme that was asked for last; <see cref="RouteableRequestedTheme.Default"/> until a view model asks for another.</summary>
    public RouteableRequestedTheme Requested { get; private set; } = RouteableRequestedTheme.Default;

    /// <inheritdoc />
    /// <remarks>The server cannot see the system theme of the visitor: it is what was asked for, <see cref="RouteableRequestedTheme.Default"/> until then.</remarks>
    public RouteableRequestedTheme Current => Requested;

    /// <summary>Raised when <see cref="Requested"/> changes, not when the same theme is asked for again.</summary>
    public event EventHandler<RouteableRequestedTheme>? RequestedThemeChanged;

    /// <inheritdoc />
    public void Request(RouteableRequestedTheme theme)
    {
        if (theme == Requested)
            return;

        Requested = theme;
        RequestedThemeChanged?.Invoke(this, theme);
    }
}

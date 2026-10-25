using Routeables.Abstraction;
using Routeables.Services;

namespace Routeables.Razor;

/// <summary>
/// Provides a concrete implementation of <see cref="IRouteableRequestedThemeService"/> to manage application themes 
/// (dark vs. light mode) and notify observers when the requested theme changes.
/// </summary>
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public class RouteableRequestThemeService : IRouteableRequestedThemeService
{
    /// <summary>
    /// Occurs when the requested theme has changed.
    /// </summary>
    public event EventHandler<RouteableRequestedTheme>? RequestedThemeChanged;

    /// <summary>
    /// Gets or sets a value indicating whether dark mode is currently enabled, defaulting to <see langword="true"/>.
    /// </summary>
    public bool IsDarkMode { get; set; } = true;

    /// <summary>
    /// Requests a new application theme, updating the dark mode state and invoking change notification handlers.
    /// </summary>
    /// <param name="theme">The requested theme.</param>
    public void Request(RouteableRequestedTheme theme)
    {
        IsDarkMode = theme == RouteableRequestedTheme.Dark;
        RequestedThemeChanged?.Invoke(this, theme);
    }
}
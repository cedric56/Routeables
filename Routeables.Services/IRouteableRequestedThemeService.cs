namespace Routeables.Services;

/// <summary>
/// The visual theme an app (or the OS) is set to use.
/// </summary>
public enum RouteableRequestedTheme
{
    /// <summary>
    /// Default, unknown or unspecified theme.
    /// </summary>
    Default,

    /// <summary>
    /// Light theme.
    /// </summary>
    Light,

    /// <summary>
    /// Dark theme.
    /// </summary>
    Dark
}

/// <summary>
/// Requests that the app switch to a given visual theme, in a
/// platform-agnostic way, so ViewModels can trigger a theme change without
/// knowing whether the app is running on Avalonia, MAUI, or Blazor.
/// </summary>
public interface IRouteableRequestedThemeService
{
    /// <summary>
    /// Requests that the app switch to <paramref name="theme"/>.
    /// </summary>
    /// <param name="theme">
    /// The theme to switch to. <see cref="RouteableRequestedTheme.Default"/>
    /// typically means "follow the OS/system theme" rather than a
    /// specific light or dark value — check the concrete platform
    /// implementation to confirm how it resolves <c>Default</c>.
    /// </param>
    void Request(RouteableRequestedTheme theme);
}
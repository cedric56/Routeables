using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace Routeables.Avalonia;

/// <summary>
/// Manager object that manages top-level states on Windows.
/// </summary>
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public interface ITopLevelStateManager
{
    /// <summary>
    /// Occurs when the application's active top-level changed.
    /// </summary>
    event EventHandler ActiveChanged;

    /// <summary>
    /// Gets the application's currently active top-level.
    /// </summary>
    /// <returns>The application's currently active <see cref="TopLevel"/> object.</returns>
    TopLevel? GetActive();

    /// <summary>
    /// Sets the new active top-level that can be retrieved with <see cref="GetActive"/>.
    /// </summary>
    /// <param name="topLevel">The <see cref="TopLevel"/> object that is activated.</param>
    void OnActivated(TopLevel topLevel);
}

/// <summary>
/// Manager object that manages top-level states on Windows.
/// </summary>
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public static class TopLevelStateManager
{
    static ITopLevelStateManager? defaultImplementation;

    /// <summary>
    /// Provides the default implementation for static usage of this API.
    /// </summary>
    public static ITopLevelStateManager Default =>
        defaultImplementation ??= new TopLevelStateManagerImplementation();

    internal static void SetDefault(ITopLevelStateManager? implementation) =>
        defaultImplementation = implementation;

    private static bool classHandlersAdded;

    internal static void Initialize()
    {
        SetDefault(new TopLevelStateManagerImplementation());

        // The class handlers are static and cannot be removed: add them once, however many times the application is
        // created (they read Default when they run).
        if (classHandlersAdded)
            return;
        classHandlersAdded = true;

        TopLevel.LoadedEvent.AddClassHandler(typeof(TopLevel), (sender, args) =>
        {
            Default.OnActivated((TopLevel)sender!);
        });
        TopLevel.GotFocusEvent.AddClassHandler(typeof(TopLevel), (sender, args) =>
        {
            Default.OnActivated((TopLevel)sender!);
        });
    }
}

static class TopLevelStateManagerExtensions
{
    /// <summary>
    /// Gets the application's currently active top-level.
    /// </summary>
    /// <param name="manager">The object to invoke this method on.</param>
    /// <param name="throwOnNull">Throws an exception if no current <see cref="TopLevel"/> can be found and this value is set to <see langword="true"/>, otherwise this method returns <see langword="null"/>.</param>
    /// <returns>The application's currently active <see cref="TopLevel"/> object.</returns>
    /// <exception cref="NullReferenceException">Thrown if no current <see cref="TopLevel"/> can be found and <paramref name="throwOnNull"/> is set to <see langword="true"/>.</exception>
    public static TopLevel? GetActive(this ITopLevelStateManager manager, bool throwOnNull)
    {
        var topLevel = manager.GetActive();
        if (throwOnNull && topLevel == null)
            throw new NullReferenceException("The active TopLevel cannot be detected. Ensure that you have called Init in your Application class.");

        return topLevel;
    }

    /// <summary>
    /// Gets the application's currently active top-level's pointer.
    /// </summary>
    /// <param name="manager">The object to invoke this method on.</param>
    /// <param name="throwOnNull">Throws an exception if no current <see cref="TopLevel"/> can be found and this value is set to <see langword="true"/>, otherwise this method returns <see cref="IntPtr.Zero"/>.</param>
    /// <returns>The application's currently active top-level's <see cref="IntPtr"/>.</returns>
    /// <exception cref="NullReferenceException">Thrown if no current <see cref="TopLevel"/> can be found and <paramref name="throwOnNull"/> is set to <see langword="true"/>.</exception>
    public static IntPtr GetActiveHandle(this ITopLevelStateManager manager, bool throwOnNull)
    {
        return manager.GetActive(throwOnNull)?.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
    }
}

class TopLevelStateManagerImplementation : ITopLevelStateManager
{
    TopLevel? _active;

    public event EventHandler? ActiveChanged;

    public void OnActivated(TopLevel topLevel)
    {
        if (_active == topLevel) return;

        if (_active is not null)
            _active.Closed -= OnActiveClosed;

        _active = topLevel;
        _active.Closed += OnActiveClosed;

        ActiveChanged?.Invoke(topLevel, EventArgs.Empty);
    }

    private void OnActiveClosed(object? sender, EventArgs e)
    {
        if (sender is not TopLevel tl || tl != _active) return;

        _active.Closed -= OnActiveClosed;
        _active = null;

        // Notify listeners that "active" changed (to null).
        // If the interface contract requires a non-null TopLevel here,
        // consider raising with the newly-resolved active instead.
        ActiveChanged?.Invoke(this, EventArgs.Empty);
    }

    public TopLevel? GetActive()
    {
        var lifetime = Application.Current?.ApplicationLifetime;

        // Prefer the live _active only if it's still valid.
        if (_active is { } a)
            return a;

        return lifetime switch
        {
            IClassicDesktopStyleApplicationLifetime desktop => TopLevel.GetTopLevel(desktop.MainWindow),
            ISingleViewApplicationLifetime singleView => TopLevel.GetTopLevel(singleView.MainView),
            _ => TopLevel.GetTopLevel(null)
        };
    }
}

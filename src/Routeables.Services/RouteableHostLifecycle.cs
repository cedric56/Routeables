using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Errors;

namespace Routeables.Services;

/// <summary>
/// What the hosts do the same way when their windows come and go, without any UI: the first activation selects the first entry of the menu,
/// the closing of the last window runs the callback of the application. Both are raised from an event, so a failure is reported and goes no further.
/// </summary>
internal static class RouteableHostLifecycle
{
    /// <summary>
    /// What a window does the first time it is activated: selects the first entry of the menu view model. It is a singleton
    /// (<c>RegisterMenuViewModel</c> refuses a transient one), so this is the instance the window displays.
    /// </summary>
    /// <param name="provider">The container of the host.</param>
    /// <param name="source">The type reported when it fails.</param>
    public static void SelectFirst(IServiceProvider provider, Type source)
    {
        ArgumentNullException.ThrowIfNull(provider);

        // Raised from an event: it must not end the application.
        try { (provider.GetService(typeof(IRouteableMenuViewModel)) as IRouteableMenuViewModel)?.SelectFirst(); }
        catch (Exception ex) { RouteableErrors.Report(provider, ex, source); }
    }

    /// <summary>Runs what the application asked for when its window closes; the container is disposed after it, so a failure is reported and goes no further.</summary>
    /// <param name="provider">The container of the host.</param>
    /// <param name="onDispose">The callback of the application, or <see langword="null"/>.</param>
    /// <param name="source">The type reported when it fails.</param>
    public static void RunOnDispose(IServiceProvider provider, Action? onDispose, Type source)
    {
        ArgumentNullException.ThrowIfNull(provider);

        try { onDispose?.Invoke(); }
        catch (Exception ex) { RouteableErrors.Report(provider, ex, source); }
    }
}

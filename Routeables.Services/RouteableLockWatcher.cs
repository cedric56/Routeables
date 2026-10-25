using Routeables.Abstraction;

namespace Routeables.Services;

/// <summary>
/// What the base views of the hosts share to follow the lock of the main view model: a single subscription to
/// <see cref="IMainViewModel.LockChanged"/>, however many times the view is initialized again, and the rule that decides
/// whether the main view model is locked.
/// </summary>
/// <remarks>
/// The main view model is a singleton that outlives the views: the watcher unsubscribes from the one it subscribed to, even
/// if the data context of the view has been replaced since. The callback runs on the thread that asked for the route change;
/// the host puts the value on its own thread.
/// </remarks>
/// <param name="changed">Called with the new value whenever the lock of the main view model changes.</param>
internal sealed class RouteableLockWatcher(Action<bool> changed) : IDisposable
{
    private IMainViewModel? _followed;

    /// <summary>Whether every entry of <paramref name="routeables"/> is locked (a page is blocking the route change); <see langword="false"/> without entries.</summary>
    /// <param name="routeables">The main view model, or <see langword="null"/>.</param>
    public static bool AllLocked(IMainViewModel? routeables) =>
        routeables?.Routeables is { Count: > 0 } entries && entries.All(entry => entry.IsLock);

    /// <summary>
    /// Follows the lock of <paramref name="context"/> when it is a main view model, and stops following the previous one.
    /// A view initialized again (a cached view shown again) therefore keeps one subscription.
    /// </summary>
    /// <param name="context">The data context of the view.</param>
    /// <returns>Whether the main view model is locked now; <see langword="false"/> when <paramref name="context"/> is not one.</returns>
    public bool Follow(object? context)
    {
        Dispose();

        if (context is not IMainViewModel routeables)
            return false;

        _followed = routeables;
        routeables.LockChanged += OnLockChanged;
        return AllLocked(routeables);
    }

    /// <summary>Stops following the lock of the main view model.</summary>
    public void Dispose()
    {
        if (_followed is not null)
            _followed.LockChanged -= OnLockChanged;

        _followed = null;
    }

    private void OnLockChanged(object? sender, EventArgs e)
    {
        if ((sender as IMainViewModel ?? _followed) is { } routeables)
            changed(AllLocked(routeables));
    }
}

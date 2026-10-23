using Routeables.Abstraction;

namespace Routeables.Routing;

/// <summary>
/// Manages routing lifecycle events (initialization, entering, leaving, transitioning, and destruction) 
/// for views and view models, preserving UI context by intentionally omitting <c>ConfigureAwait(false)</c>.
/// </summary>
internal sealed class RouterLifecycle(IServiceProvider? provider = null)
{
    /// <summary>
    /// Checks whether the specified entry's view model and view allow leaving via routing.
    /// </summary>
    /// <param name="entry">The router entry being left.</param>
    /// <param name="ct">A token to observe for cancellation.</param>
    /// <returns><see langword="true"/> if routing away is permitted; otherwise, <see langword="false"/>.</returns>
    public async Task<bool> CanLeaveAsync(RouterEntry entry, CancellationToken ct = default)
    {
        if (entry.ViewModel is IRouteable vm && !await vm.CanLeaveAsync(ct)) return false;
        if (entry.View is IRouteable v && !await v.CanLeaveAsync(ct)) return false;
        return true;
    }

    /// <summary>
    /// Executes a transition from a leaving entry to an entering entry, with fallback recovery if entering fails.
    /// </summary>
    /// <param name="leaving">The entry currently active that is being left.</param>
    /// <param name="entering">The new entry being routed to.</param>
    /// <param name="ct">A token to observe for cancellation.</param>
    public async Task TransitionAsync(RouterEntry leaving, RouterEntry entering, CancellationToken ct = default)
    {
        await LeaveAsync(leaving, ct);

        try
        {
            await EnterAsync(entering, ct);
        }
        catch
        {
            // `entering` failed partway through OnEntering. `leaving` has already torn down, but the
            // router does not advance history on a failed transition, so history still points at
            // `leaving`. Re-run its OnEntering so its lifecycle matches "still current" again.
            try
            {
                await EnterAsync(leaving, CancellationToken.None);
            }
            catch (Exception recovery)
            {
                // Recovery itself failed. Do not hide the original exception: it is thrown below; this one is reported.
                RouteableErrors.Report(provider, "RouterLifecycle.Recover", recovery);
            }

            throw;
        }
    }

    /// <summary>
    /// Notifies the view model and view of an entry that route change is leaving them.
    /// </summary>
    private static async Task LeaveAsync(RouterEntry entry, CancellationToken ct)
    {
        if (entry.ViewModel is IRouteable vm) await vm.OnLeavingAsync(entry.Args, ct);
        if (entry.View is IRouteable v) await v.OnLeavingAsync(entry.Args, ct);
    }

    /// <summary>
    /// Notifies the view model and view of an entry that route change is entering them.
    /// </summary>
    /// <param name="entry">The entry being entered.</param>
    /// <param name="ct">A token to observe for cancellation.</param>
    internal async Task EnterAsync(RouterEntry entry, CancellationToken ct = default)
    {
        if (entry.ViewModel is IRouteable vm) await vm.OnEnteringAsync(entry.Args, ct);
        if (entry.View is IRouteable v) await v.OnEnteringAsync(entry.Args, ct);
    }

    /// <summary>
    /// Tells a committed route change to the entries involved: <see cref="IRouteable.OnLeftAsync"/> on the entry
    /// that was left (view model, then view), then <see cref="IRouteable.OnEnteredAsync"/> on the entry now current.
    /// The history has already moved, so a failing callback cannot undo or fail the route change: its exception is
    /// reported to the debug output and the remaining callbacks still run. An instance that is both view and view model
    /// is notified once per entry.
    /// </summary>
    /// <param name="leaving">The entry that was left, or <see langword="null"/> on the first route change.</param>
    /// <param name="entering">The entry now current.</param>
    /// <param name="ct">A token to observe for cancellation.</param>
    public async Task NotifyEnteredAsync(RouterEntry? leaving, RouterEntry entering, CancellationToken ct = default)
    {
        if (leaving is not null)
        {
            await NotifyAsync(leaving, (r, args) => r.OnLeftAsync(args, ct));
        }
        await NotifyAsync(entering, (r, args) => r.OnEnteredAsync(args, ct));
    }

    private async Task NotifyAsync(RouterEntry entry, Func<IRouteable, IRouteableArgs, Task> call)
    {
        foreach (var target in entry.ViewModel is not null && ReferenceEquals(entry.ViewModel, entry.View)
                     ? new[] { entry.ViewModel }
                     : new[] { entry.ViewModel, entry.View })
        {
            if (target is not IRouteable routeable) continue;
            try { await call(routeable, entry.Args); }
            catch (Exception ex)
            {
                // Same road as every other error of the library: the sink the application registered, a trace otherwise.
                RouteableErrors.Report(provider, $"{target.GetType().Name}.OnEntered/OnLeft", ex);
            }
        }
    }

    /// <summary>
    /// Initializes the view model and view of a target entry asynchronously: first <see cref="IInitializeAsync"/>
    /// with the route change arguments, then <see cref="IInitializeAsync{TData}"/> with the typed data, if any.
    /// </summary>
    /// <param name="entry">The entry to initialize.</param>
    /// <param name="ct">A token to observe for cancellation.</param>
    public async Task InitializeAsync(RouterEntry entry, CancellationToken ct = default)
    {
        if (entry.ViewModel is IInitializeAsync vm)
            await vm.InitializeAsync(entry.Args, ct);

        if (entry.View is IInitializeAsync v && !ReferenceEquals(v, entry.ViewModel))
            await v.InitializeAsync(entry.Args, ct);

        // Typed data supplied through GoToRouteAsync<TViewModel, TData> (see RouterTypedExtensions).
        if (TypedInitialization.Find(entry.Args) is { } typed)
        {
            if (typed.Accepts(entry.ViewModel))
                await typed.InitializeAsync(entry.ViewModel, ct);

            if (!ReferenceEquals(entry.View, entry.ViewModel) && typed.Accepts(entry.View))
                await typed.InitializeAsync(entry.View, ct);
        }
    }

    /// <summary>
    /// Disposes removed router entries, ensuring that shared or remaining instances (such as singletons or cached views) are preserved.
    /// </summary>
    // What this lifecycle has disposed. Weak: it must not keep the pages alive. A menu entry that remembers its page
    // asks it (WasDestroyed) before showing that page again.
    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<object, object> _destroyed = new();

    /// <summary>Whether the router disposed <paramref name="instance"/> when it left the history.</summary>
    public bool WasDestroyed(object? instance) => instance is not null && _destroyed.TryGetValue(instance, out _);

    public async Task DestroyAsync(IReadOnlyList<RouterEntry> removed, IReadOnlyList<RouterEntry> remaining)
    {
        if (removed.Count == 0) return;

        var alive = new HashSet<object>(ReferenceEqualityComparer.Instance);
        foreach (var e in remaining) { alive.Add(e.ViewModel); alive.Add(e.View); }

        // Singleton and scoped pages belong to their cache, which hands them out again and disposes them with the provider
        // or the scope: leaving the history must not dispose what the next route change will show.
        var caches = new object?[] { provider?.GetService(typeof(ScopedViewCache)), provider?.GetService(typeof(SingletonViewCache)) }
            .OfType<ScopedViewCache>().ToArray();

        var done = new HashSet<object>(ReferenceEqualityComparer.Instance);
        foreach (var e in removed)
        {
            foreach (var target in new[] { e.ViewModel, e.View })
            {
                if (alive.Contains(target) || !done.Add(target) || caches.Any(c => c.Owns(target))) continue;
                try
                {
                    if (target is IAsyncDisposable ad) { _destroyed.TryAdd(target, target); await ad.DisposeAsync(); }
                    else if (target is IDisposable d) { _destroyed.TryAdd(target, target); d.Dispose(); }
                }
                catch (Exception ex) { RouteableErrors.Report(provider, "RouterLifecycle.Destroy", ex); }   // cleanup must never fail a navigation
            }
        }
    }
}
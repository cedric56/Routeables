using Routeables.Abstraction.History;
using Routeables.Abstraction.Routing;

namespace Routeables.Routing;

// Moves inside the history: back, forward, pop, move to an entry, erase.
internal partial class Router
{

    // The gate is entered inline in each public method, not through an async helper: its AsyncLocal flag must be set in the method's own flow,
    // so that the callbacks awaited below can see it.
    // Call only while the gate is held.
    private async Task<RouteableResult> MoveAsync(
        RouteableKind kind,
        RouterEntry? target,
        Func<IReadOnlyList<RouterEntry>> commit,
        CancellationToken ct)
    {
        var leaving = history.Current;
        if (leaving is null || target is null || ReferenceEquals(leaving, target))
            return RouteableResult.Cancel(); // nothing to do: no events

        return await ObserveAsync(kind, leaving, target, ct, async () =>
        {
            if (!await lifecycle.CanLeaveAsync(leaving, ct)) return RouteableResult.Cancel();

            await lifecycle.TransitionAsync(leaving, target, ct); // if this throws, the history is untouched

            // A hook that ignores its token must not let a replaced route change into the history.
            ct.ThrowIfCancellationRequested();
            var removed = commit(); // only after the lifecycle succeeded
            await lifecycle.NotifyEnteredAsync(leaving, target, ct); // before the removed entries are disposed
            // Nothing removed, nothing to destroy: copying the whole history is only needed when entries are removed.
            if (removed.Count > 0) await lifecycle.DestroyAsync(removed, history.Snapshot());
            return RouteableResult.Ok();
        });
    }

    // The shape of every navigation: wait for the gate, run, release it. EnterAsync is deliberately not async (its AsyncLocal must be set in
    // the flow that awaits the body, so a nested navigation is detected), and this helper is that flow: the body is awaited inside it.
    private async Task<T> RunGatedAsync<T>(CancellationToken cancellationToken, Func<CancellationToken, Task<T>> body)
    {
        var enter = gate.EnterAsync(cancellationToken);
        try { await enter; } catch { gate.Abort(); throw; }
        try
        {
            var token = gate.Observe(cancellationToken);
            // Under CancelCurrent a newer route change may have cancelled this one between taking the gate and starting: it never starts.
            token.ThrowIfCancellationRequested();
            return await body(token);
        }
        finally { gate.Exit(); }
    }

    /// <inheritdoc />
    public async Task<RouteableResult> GoBackAsync(CancellationToken cancellationToken = default)
    {
        return await RunGatedAsync(cancellationToken, token =>
            MoveAsync(RouteableKind.Back, history.PeekBack(),
                () => { history.MoveBack(); return []; }, token));
    }

    /// <inheritdoc />
    public async Task<RouteableResult> ForwardAsync(CancellationToken cancellationToken = default)
    {
        return await RunGatedAsync(cancellationToken, token =>
            MoveAsync(RouteableKind.Forward, history.PeekForward(),
                () => { history.MoveForward(); return []; }, token));
    }

    /// <inheritdoc />
    public async Task<RouteableResult> GoBackToRootAsync(CancellationToken cancellationToken = default)
    {
        return await RunGatedAsync(cancellationToken, async token =>
        {
            // Already on the root with entries after it (it went back to it): nothing moves, but the entries after it are still discarded, as when the
            // cursor comes from further away. Otherwise they, and their pages, would be kept for a forward that was meant to be forgotten.
            if (history.Current is { } current && ReferenceEquals(current, history.Root) && history.Count > 1)
            {
                var removed = history.MoveToRoot();
                await lifecycle.DestroyAsync(removed, history.Snapshot());
                return RouteableResult.Ok();
            }

            return await MoveAsync(RouteableKind.Root, history.Root, history.MoveToRoot, token);
        });
    }

    /// <inheritdoc />
    public async Task<RouteableResult> PopAsync(CancellationToken cancellationToken = default)
    {
        return await RunGatedAsync(cancellationToken, token =>
            MoveAsync(RouteableKind.Pop, history.PeekBack(),
                history.Pop, token));
    }

    /// <inheritdoc />
    public async Task<RouteableResult> MoveToAsync(int index, CancellationToken cancellationToken = default)
    {
        return await RunGatedAsync(cancellationToken, token =>
            MoveAsync(RouteableKind.MoveTo, history.At(index),
                () => { history.MoveTo(index); return []; }, token));
    }

    /// <inheritdoc />
    public Task<RouteableResult> MoveBackAsync(Func<IHistoryEntry, bool> match, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(match);
        return MoveBackCoreAsync(entries =>
        {
            for (var i = history.CurrentIndex - 1; i >= 0 && i < entries.Count; i--)
                if (match(entries[i])) return i;
            return -1;
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<RouteableResult> MoveBackAsync(int steps, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(steps, 1);
        return MoveBackCoreAsync(_ => history.CurrentIndex - steps, cancellationToken);
    }

    // The target is chosen while holding the gate: after the navigations already running, from the history as it is then.
    private async Task<RouteableResult> MoveBackCoreAsync(Func<IReadOnlyList<IHistoryEntry>, int> find, CancellationToken cancellationToken)
    {
        return await RunGatedAsync(cancellationToken, token =>
        {
            var index = find(((IRouterHistory)history).Entries);
            if (index < 0 || index >= history.Count)
                return Task.FromResult(RouteableResult.Cancel());

            return MoveAsync(RouteableKind.MoveTo, history.At(index),
                () => { history.MoveTo(index); return []; }, token);
        });
    }

    /// <inheritdoc />
    public async Task EraseHistoryAsync(CancellationToken cancellationToken = default)
    {
        await RunGatedAsync<object?>(cancellationToken, async _ =>
        {
            // The page shown stops being current. Tell it, as for any other navigation, so that whatever it took in OnEntering is given back
            // (a page kept in a cache is not disposed below, and would otherwise be entered twice in a row).
            var shown = history.Current;
            if (shown is not null)
                await lifecycle.NotifyLeavingAsync(shown);
            var removed = history.Clear();
            // Tell the page it was left before destroying it, as in any other navigation: a disposed page no longer has what OnLeftAsync gives back.
            if (shown is not null)
                await lifecycle.NotifyLeftAsync(shown);
            await lifecycle.DestroyAsync(removed, history.Snapshot()); // the snapshot is empty now
            return null;
        });
    }
}

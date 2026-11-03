namespace Routeables.Abstraction;

/// <summary>
/// Opens a page and waits for the value it gives back: routing with a result, for a page the user comes back from (a picker,
/// a form, a confirmation). It works on any router (a region too) and needs nothing from the router itself.
/// <code>
/// var picked = await router.GoToForResultAsync&lt;Color&gt;("/colors");
/// if (picked.Returned) Apply(picked.Value);
/// </code>
/// </summary>
public static class RouterResultExtensions
{
    /// <summary>
    /// Goes to <paramref name="url"/> (see <see cref="IRouter.GoToAsync"/>) and completes when that page is left for good
    /// (<see cref="IRouter.PopAsync"/>, <see cref="IRouter.GoBackAsync"/>, the history cleared), not when it opens another page on top
    /// of itself. The value comes from the page's <see cref="IReturnsResult{TResult}"/>.
    /// </summary>
    public static Task<RouteableResult<TResult>> GoToForResultAsync<TResult>(
        this IRouter router, string url, IRouteableArgs? args = null, string? template = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(router);
        return router.WaitForResultAsync<TResult>(() => router.GoToAsync(url, args, template, cancellationToken), cancellationToken);
    }

    private static async Task<RouteableResult<TResult>> WaitForResultAsync<TResult>(
        this IRouter router, Func<Task<RouteableResult>> navigate, CancellationToken cancellationToken)
    {
        // Completed from the handler of the history, which runs while the router holds its gate: whoever awaits must not run there.
        var done = new TaskCompletionSource<RouteableResult<TResult>>(TaskCreationOptions.RunContinuationsAsynchronously);

        // The page is watched from the moment it opens, not once the route change call returns: by then it may already have been left
        // (popped by whoever saw it open), and History.Current would be another page.
        var before = router.History.Current;
        IHistoryEntry? entry = null;
        RouteableResult? navigation = null;

        // The page is left when it is no longer between the first entry and the current one: popped, gone back from, cleared. A page
        // that opened another one on top of itself is still in that range.
        bool IsLeft() => entry is not null
            && !router.History.Entries.Take(router.History.CurrentIndex + 1).Any(e => ReferenceEquals(e, entry));

        void OnChanged(object? sender, EventArgs e)
        {
            if (entry is null)
            {
                if (router.History.Current is { } current && !ReferenceEquals(current, before))
                    entry = current;
                return;
            }
            if (!IsLeft()) return;

            // Read now: the entries that left the history are disposed right after.
            done.TrySetResult(Read<TResult>(entry, navigation ?? new RouteableResult { Success = true }));
        }

        router.History.Changed += OnChanged;
        using var registration = cancellationToken.Register(() =>
            done.TrySetResult(new RouteableResult<TResult> { OpenResult = navigation ?? new RouteableResult(), Cancelled = true }));
        try
        {
            navigation = await navigate();
            if (!navigation.Success)
                return new RouteableResult<TResult> { OpenResult = navigation };

            entry ??= router.History.Current;
            if (entry is null)
                return new RouteableResult<TResult> { OpenResult = navigation };

            if (IsLeft())      // left between the navigation and now
                done.TrySetResult(Read<TResult>(entry, navigation));

            return await done.Task.ConfigureAwait(false);
        }
        finally
        {
            router.History.Changed -= OnChanged;
        }
    }

    private static RouteableResult<TResult> Read<TResult>(IHistoryEntry entry, RouteableResult navigation)
    {
        foreach (var target in new[] { entry.ViewModel, entry.View })
        {
            if (target is IReturnsResult<TResult> source && source.TryGetResult(out var value))
                return new RouteableResult<TResult> { OpenResult = navigation, Returned = true, Value = value };
        }

        return new RouteableResult<TResult> { OpenResult = navigation };
    }
}

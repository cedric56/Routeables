using Routeables.Abstraction.History;

namespace Routeables.Abstraction.Routing;

/// <summary>Moves through several pages at once: rebuild a stack (deep link, wizard) or go back to an earlier page.</summary>
public static class RouterStackExtensions
{
    /// <summary>
    /// Goes to each route in order, so that the history ends with one entry per route: <c>GoToStackAsync(["/orders", "/orders/42", "/orders/42/edit"])</c>.
    /// Every page goes through the normal lifecycle and guards (the intermediate pages are entered, then left). Stops at the first route
    /// that fails or is cancelled and returns that result; the pages already reached stay in the history.
    /// </summary>
    public static async Task<RouteableResult> GoToStackAsync(
        this IRouter router, IEnumerable<string> routes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(router);
        ArgumentNullException.ThrowIfNull(routes);
        var result = RouteableResult.Cancel();   // an empty list changes nothing
        foreach (var route in routes)
        {
            result = await router.GoToAsync(route, cancellationToken: cancellationToken);
            if (!result.Success) return result;
        }
        return result;
    }

    /// <summary>
    /// Goes back to the nearest earlier entry opened with <paramref name="route"/> (compared without the leading <c>/</c>, the query and the case).
    /// The entries after it stay in the history, as with <see cref="IRouter.GoBackAsync"/>. Cancelled when no earlier entry matches.
    /// </summary>
    public static Task<RouteableResult> GoBackToAsync(this IRouter router, string route, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(route);
        static string Normal(string? r) => (r ?? string.Empty).Split('?')[0].Trim('/');
        var wanted = Normal(route);
        return router.GoBackToAsync(e => string.Equals(Normal(e.Args.Route), wanted, StringComparison.OrdinalIgnoreCase), cancellationToken);
    }

    /// <summary>Goes back to the nearest earlier entry whose view model is a <typeparamref name="TViewModel"/>.</summary>
    public static Task<RouteableResult> GoBackToAsync<TViewModel>(this IRouter router, CancellationToken cancellationToken = default)
        => router.GoBackToAsync(e => e.ViewModel is TViewModel, cancellationToken);

    /// <summary>Goes back to the nearest earlier entry that satisfies <paramref name="match"/>. Cancelled when none does.</summary>
    public static Task<RouteableResult> GoBackToAsync(this IRouter router, Func<IHistoryEntry, bool> match, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(router);
        ArgumentNullException.ThrowIfNull(match);
        var entries = router.History.Entries;
        for (var i = router.History.CurrentIndex - 1; i >= 0 && i < entries.Count; i--)
            if (match(entries[i])) return router.MoveToAsync(i, cancellationToken);
        return Task.FromResult(RouteableResult.Cancel());
    }

    /// <summary>Goes back <paramref name="steps"/> entries at once (one lifecycle change, not one per page). Cancelled when the history is shorter.</summary>
    public static Task<RouteableResult> GoBackAsync(this IRouter router, int steps, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(router);
        ArgumentOutOfRangeException.ThrowIfLessThan(steps, 1);
        var index = router.History.CurrentIndex - steps;
        return index < 0 ? Task.FromResult(RouteableResult.Cancel()) : router.MoveToAsync(index, cancellationToken);
    }
}

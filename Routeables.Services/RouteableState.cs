using Routeables.Abstraction;

namespace Routeables.Services;

/// <summary>
/// Saves the pages a router shows as a list of routes, and takes the user back to them: after the system killed the process of an
/// application that was in the background (Android does), or at the next start.
/// </summary>
/// <remarks>
/// What is saved is the address of each page, not the page: its view model is built again from its route and its arguments, so what was typed
/// in a form or loaded in memory is not brought back, unless the view model implements <see cref="IRouteableState"/> and writes it
/// (<see cref="CaptureSnapshots"/>). A page that was reached with values that are not in its address (<c>RouteableArgs.WithValue</c>) is
/// reached without them. Pages that have no address (a view and a view model given to <see cref="IRouter.GoAsync"/>) are not saved. The
/// host stores the result where it keeps its state (an Android bundle, a file, <c>Preferences</c>) as a string
/// (<c>Serialize</c> and <c>Deserialize</c>).
/// </remarks>
public static class RouteableState
{
    /// <summary>The routes of the pages of the history, from the first one to the current one, oldest first. The pages that can be reached with Forward are not part of it.</summary>
    public static IReadOnlyList<string> Capture(IRouter router)
    {
        ArgumentNullException.ThrowIfNull(router);

        var history = router.History;
        var entries = history.Entries;
        var last = Math.Min(history.CurrentIndex, entries.Count - 1);

        var routes = new List<string>();
        for (var i = 0; i <= last; i++)
        {
            if (entries[i].Args.Route is { Length: > 0 } route)
                routes.Add(route);
        }

        return routes;
    }

    /// <summary>
    /// Replaces the history by the pages of <paramref name="routes"/>, in order, the last one being the page shown. Nothing changes
    /// when the list is empty. A route that cannot be reached (a page that no longer exists, a refusal of a guard) ends the restoration:
    /// the pages before it stay.
    /// </summary>
    /// <returns><see langword="true"/> when every route was reached.</returns>
    public static async Task<bool> RestoreAsync(IRouter router, IEnumerable<string> routes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(router);
        ArgumentNullException.ThrowIfNull(routes);

        var list = routes.Where(route => !string.IsNullOrWhiteSpace(route)).ToList();
        if (list.Count == 0)
            return true;

        await router.EraseHistoryAsync(cancellationToken);
        foreach (var route in list)
        {
            var result = await router.GoToAsync(route, null, null, cancellationToken);
            if (!result.Success)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Like <see cref="Capture(IRouter)"/>, and the values each view model that implements <see cref="IRouteableState"/> wants to keep.
    /// </summary>
    public static IReadOnlyList<RouteableSnapshot> CaptureSnapshots(IRouter router)
    {
        ArgumentNullException.ThrowIfNull(router);

        var history = router.History;
        var entries = history.Entries;
        var last = Math.Min(history.CurrentIndex, entries.Count - 1);

        var snapshots = new List<RouteableSnapshot>();
        for (var i = 0; i <= last; i++)
        {
            if (entries[i].Args.Route is not { Length: > 0 } route)
                continue;

            var values = new Dictionary<string, string>();
            if (entries[i].ViewModel is IRouteableState keeper)
                keeper.SaveState(values);
            snapshots.Add(new RouteableSnapshot(route, values));
        }

        return snapshots;
    }

    /// <summary>
    /// Like <see cref="RestoreAsync"/>, and each page that has values gets them back
    /// (<see cref="IRouteableState.LoadState"/>) once it is shown.
    /// </summary>
    public static async Task<bool> RestoreSnapshotsAsync(IRouter router, IEnumerable<RouteableSnapshot> snapshots, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(router);
        ArgumentNullException.ThrowIfNull(snapshots);

        var list = snapshots.Where(snapshot => !string.IsNullOrWhiteSpace(snapshot.Route)).ToList();
        if (list.Count == 0)
            return true;

        await router.EraseHistoryAsync(cancellationToken);
        foreach (var snapshot in list)
        {
            var result = await router.GoToAsync(snapshot.Route, null, null, cancellationToken);
            if (!result.Success)
                return false;

            // Not when a guard sent the user to another page: those values are not for it.
            if (snapshot.State.Count > 0
                && string.Equals(router.History.Current?.Args.Route, snapshot.Route, StringComparison.Ordinal)
                && router.CurrentViewModel is IRouteableState keeper)
                keeper.LoadState(snapshot.State);
        }

        return true;
    }

    /// <summary>The pages and their values as one string, to keep in a setting or a file.</summary>
    public static string Serialize(IEnumerable<RouteableSnapshot> snapshots)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        return string.Join('\n', snapshots.Select(snapshot => string.Concat(
            Uri.EscapeDataString(snapshot.Route),
            string.Concat(snapshot.State.Select(pair => $"|{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}")))));
    }

    /// <summary>The pages and their values of a string made by <see cref="Serialize(IEnumerable{RouteableSnapshot})"/> or <see cref="Serialize(IEnumerable{string})"/>.</summary>
    public static IReadOnlyList<RouteableSnapshot> DeserializeSnapshots(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return [];

        var snapshots = new List<RouteableSnapshot>();
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split('|');
            var values = new Dictionary<string, string>();
            foreach (var part in parts.Skip(1))
            {
                var at = part.IndexOf('=');
                if (at > 0)
                    values[Uri.UnescapeDataString(part[..at])] = Uri.UnescapeDataString(part[(at + 1)..]);
            }

            snapshots.Add(new RouteableSnapshot(Uri.UnescapeDataString(parts[0]), values));
        }

        return snapshots;
    }

    /// <summary>The routes as one string (one escaped route per line), to keep in a setting or a file.</summary>
    public static string Serialize(IEnumerable<string> routes)
    {
        ArgumentNullException.ThrowIfNull(routes);
        return string.Join('\n', routes.Select(Uri.EscapeDataString));
    }

    /// <summary>The routes of a string made by <c>Serialize</c>; an empty or <see langword="null"/> string gives no route.</summary>
    public static IReadOnlyList<string> Deserialize(string? text)
        => string.IsNullOrEmpty(text)
            ? []
            : [.. DeserializeSnapshots(text).Select(snapshot => snapshot.Route)];
}

/// <summary>A page of the history: its route and the values its view model keeps (<see cref="IRouteableState"/>).</summary>
/// <param name="Route">The route of the page.</param>
/// <param name="State">The values; empty for a page that keeps none.</param>
public sealed record RouteableSnapshot(string Route, IReadOnlyDictionary<string, string> State)
{
    /// <summary>A page with no values.</summary>
    public RouteableSnapshot(string route) : this(route, new Dictionary<string, string>()) { }
}

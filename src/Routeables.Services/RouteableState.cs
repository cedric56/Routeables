using Routeables.Abstraction;

namespace Routeables.Services;

/// <summary>
/// Saves the pages a router shows as a list of routes, and takes the user back to them: after the system killed the process of an
/// application that was in the background (Android does), or at the next start.
/// </summary>
/// <remarks>
/// What is saved is the address of each page, not the page: its view model is built again from its route and its arguments, so what was typed
/// in a form or loaded in memory is not brought back, unless the view model implements <see cref="IRouteableState"/> and writes it
/// (<see cref="CaptureSnapshots"/>). The values a page was reached with that are not in its address (<c>RouteableArgs.WithValue</c>) are kept
/// with the snapshot when they are text, numbers, <c>bool</c>, <c>Guid</c> or dates (<see cref="CaptureSnapshots"/>), and given again when
/// the page is reached again: each page of the history owns its values. Other values are not kept. Pages that have no address (a view and a view model given to <see cref="IRouter.GoAsync"/>) are not saved. The
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
            CaptureValues(entries[i].Args, route, values);
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
            var result = await router.GoToAsync(snapshot.Route, ValuesArgs(snapshot.State), null, cancellationToken);
            if (!result.Success)
                return false;

            // Not when a guard sent the user to another page: those values are not for it.
            if (snapshot.State.Count > 0
                && string.Equals(router.History.Current?.Args.Route, snapshot.Route, StringComparison.Ordinal)
                && router.CurrentViewModel is IRouteableState keeper)
                keeper.LoadState(snapshot.State.Where(pair => !pair.Key.StartsWith(ValuePrefix, StringComparison.Ordinal))
                    .ToDictionary(pair => pair.Key, pair => pair.Value));
        }

        return true;
    }


    // The values of a page that are not in its address (RouteableArgs.WithValue) are kept in the same dictionary as what its view model
    // saves, under a prefix: "value:<type>:<name>". Only what can be written as text and read back is kept (text, numbers, bool, Guid,
    // dates); the values the route itself brings (its query) come back with the route.
    internal const string ValuePrefix = "value:";

    private static void CaptureValues(IRouteableArgs args, string route, Dictionary<string, string> into)
    {
        var inAddress = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (route.IndexOf('?') is var at and >= 0)
        {
            foreach (var pair in route[(at + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries))
                inAddress.Add(Uri.UnescapeDataString(pair.Split('=', 2)[0]));
        }

        foreach (var (value, name, _) in args.Values)
        {
            if (string.IsNullOrEmpty(name) || inAddress.Contains(name) || ValueTag(value) is not { } tag)
                continue;
            into[$"{ValuePrefix}{tag}:{name}"] = Format(value!);
        }
    }

    private static IRouteableArgs? ValuesArgs(IReadOnlyDictionary<string, string> state)
    {
        RestoredArgs? args = null;
        foreach (var (key, text) in state)
        {
            if (!key.StartsWith(ValuePrefix, StringComparison.Ordinal))
                continue;
            var rest = key[ValuePrefix.Length..];
            var colon = rest.IndexOf(':');
            if (colon <= 0 || Parse(rest[..colon], text) is not { } value)
                continue;
            args ??= new RestoredArgs();
            args.Values.Add(new RouteableArgument(value, rest[(colon + 1)..]));
        }

        return args;
    }

    private static string? ValueTag(object? value) => value switch
    {
        string => "s",
        int => "i",
        long => "l",
        bool => "b",
        double => "d",
        decimal => "m",
        Guid => "g",
        DateTime => "t",
        DateTimeOffset => "o",
        _ => null,
    };

    private static string Format(object value) => value switch
    {
        DateTime time => time.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
        DateTimeOffset moment => moment.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };

    private static object? Parse(string tag, string text)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        const System.Globalization.DateTimeStyles round = System.Globalization.DateTimeStyles.RoundtripKind;
        return tag switch
        {
            "s" => text,
            "i" => int.TryParse(text, System.Globalization.NumberStyles.Integer, inv, out var i) ? i : null,
            "l" => long.TryParse(text, System.Globalization.NumberStyles.Integer, inv, out var l) ? l : null,
            "b" => bool.TryParse(text, out var b) ? b : null,
            "d" => double.TryParse(text, System.Globalization.NumberStyles.Float, inv, out var d) ? d : null,
            "m" => decimal.TryParse(text, System.Globalization.NumberStyles.Number, inv, out var m) ? m : null,
            "g" => Guid.TryParse(text, out var g) ? g : null,
            "t" => DateTime.TryParse(text, inv, round, out var t) ? t : null,
            "o" => DateTimeOffset.TryParse(text, inv, round, out var o) ? o : null,
            _ => null,
        };
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

    /// <summary>Two snapshots are equal when they have the same route and the same values (the state is data: it is compared by what it holds).</summary>
    public bool Equals(RouteableSnapshot? other)
        => other is not null
           && string.Equals(Route, other.Route, StringComparison.Ordinal)
           && State.Count == other.State.Count
           && State.All(pair => other.State.TryGetValue(pair.Key, out var value) && string.Equals(pair.Value, value, StringComparison.Ordinal));

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Route, StringComparer.Ordinal);
        // Order of the values does not count: the entries are combined by addition.
        var values = 0;
        foreach (var pair in State)
            values += HashCode.Combine(pair.Key, pair.Value);
        hash.Add(values);
        return hash.ToHashCode();
    }
}

/// <summary>The arguments of a page that is reached again: only the values it was reached with. The router copies them.</summary>
internal sealed class RestoredArgs : IRouteableArgs
{
    public List<RouteableArgument> ViewModelParameters { get; } = [];
    public List<RouteableArgument> ViewParameters { get; } = [];
    public List<RouteableArgument> Values { get; } = [];
    public Exception? Exception { get; set; }
    public string? Route { get; set; }

    public IRouteableArgs ForViewModel(params RouteableArgument[] entries) { ViewModelParameters.AddRange(entries); return this; }
    public IRouteableArgs ForValues(params RouteableArgument[] entries) { Values.AddRange(entries); return this; }
    public IRouteableArgs ForView(params RouteableArgument[] entries) { ViewParameters.AddRange(entries); return this; }
    public IRouteableArgs WithRoute(string? route = null) { Route = route; return this; }
}

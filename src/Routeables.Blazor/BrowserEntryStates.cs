using System.Runtime.CompilerServices;
using Routeables.Abstraction.History;

namespace Routeables.Blazor;

/// <summary>
/// The state each router entry gives to its browser entry (<c>history.state</c>). An address can be in the history twice ([home, detail, home]), the
/// state cannot, so the Back and the Forward of the browser are told apart by it. The entries are those of the router, kept while it keeps them.
/// </summary>
internal sealed class BrowserEntryStates
{
    private const string Prefix = "routeables:";

    // The state outlives the page (a reload keeps history.state) while the counter starts again: the states of this run carry a name of their own,
    // so that an entry left by a previous run is never taken for one of the router's.
    private static readonly string Run = Guid.NewGuid().ToString("N")[..8];
    private static long _last;

    private readonly ConditionalWeakTable<IHistoryEntry, string> _states = new();

    /// <summary>The state of a router entry, given the first time it is asked for.</summary>
    public string StateOf(IHistoryEntry entry)
        => _states.GetValue(entry, static _ => Prefix + Run + ":" + Interlocked.Increment(ref _last).ToString(System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>The router entry a browser state names, among <paramref name="entries"/>: -1 when the state is not one of this application, or names none of them.</summary>
    public int IndexOf(string? state, IReadOnlyList<IHistoryEntry> entries)
    {
        if (state is null || !state.StartsWith(Prefix, StringComparison.Ordinal))
            return -1;
        for (var i = 0; i < entries.Count; i++)
            if (_states.TryGetValue(entries[i], out var known) && string.Equals(known, state, StringComparison.Ordinal))
                return i;
        return -1;
    }
}

namespace Routeables.Abstraction;

/// <summary>Read-only view of one history entry.</summary>
public interface IHistoryEntry
{
    /// <summary>The view model of this entry.</summary>
    object ViewModel { get; }

    /// <summary>The view of this entry.</summary>
    object View { get; }

    /// <summary>The arguments the route change to this entry was made with.</summary>
    IRouteableArgs Args { get; }
}

/// <summary>Read-only view of a router's routing history.</summary>
public interface IRouterHistory
{
    /// <summary>Number of entries currently stored.</summary>
    int Count { get; }

    /// <summary>The most entries the history keeps, or <see langword="null"/> when it has no limit (the default).</summary>
    int? MaxSize { get; }

    /// <summary>Index of <see cref="Current"/>, or -1 when empty.</summary>
    int CurrentIndex { get; }

    /// <summary>Whether an entry exists before <see cref="Current"/>.</summary>
    bool CanGoBack { get; }

    /// <summary>Whether an entry exists after <see cref="Current"/>, one that was left with <c>GoBackAsync</c>.</summary>
    bool CanGoForward { get; }

    /// <summary>The first entry, or <see langword="null"/> when the history is empty.</summary>
    IHistoryEntry? Root { get; }

    /// <summary>The entry the cursor is on, or <see langword="null"/> when the history is empty.</summary>
    IHistoryEntry? Current { get; }

    /// <summary>Snapshot copy, oldest first. Safe to enumerate while routing continues.</summary>
    IReadOnlyList<IHistoryEntry> Entries { get; }

    /// <summary>
    /// Raised after any change (push, back, forward, root, replace, clear). Raised while the router
    /// gate is held, so handlers must not start a route change; update bindings or schedule work instead.
    /// </summary>
    event EventHandler? Changed;
}
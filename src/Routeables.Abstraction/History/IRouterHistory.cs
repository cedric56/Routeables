namespace Routeables.Abstraction.History;

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
    /// The entries and the index of the current one, read together. Reading <see cref="Entries"/> and <see cref="CurrentIndex"/> one after the
    /// other can mix two states when a route change runs between them; use this when both are needed. The default reads them one after the other:
    /// a history that can change from another thread overrides it.
    /// </summary>
    /// <returns>The entries, oldest first, and the index of the current one (-1 when empty).</returns>
    (IReadOnlyList<IHistoryEntry> Entries, int CurrentIndex) Capture()
    {
        var entries = Entries;
        return (entries, CurrentIndex);
    }

    /// <summary>
    /// Raised after any change (push, back, forward, root, replace, clear). Raised while the router
    /// gate is held, so handlers must not start a route change; update bindings or schedule work instead.
    /// </summary>
    event EventHandler? Changed;
}
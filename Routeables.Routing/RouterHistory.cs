using Routeables.Abstraction;

namespace Routeables.Routing;

/// <summary>
/// Manages the route change history stack, tracking the active index
/// and providing stack manipulation methods such as pushing, popping, traversing, and clearing.
/// Without a limit the stack grows with the route changes until an entry is popped or the history is erased;
/// with a <c>maxSize</c> the oldest entry is dropped when a push goes beyond it.
/// </summary>
internal sealed class RouterHistory : IRouterHistory
{
    private readonly List<RouterEntry> _entries = [];

    /// <summary>Creates a history; <paramref name="maxSize"/> is the most entries it keeps, or <see langword="null"/> for no limit.</summary>
    public RouterHistory(int? maxSize = null)
    {
        if (maxSize is < 1) throw new ArgumentOutOfRangeException(nameof(maxSize), maxSize, "The limit must be at least 1.");
        MaxSize = maxSize;
    }

    /// <summary>The most entries the history keeps, or <see langword="null"/> when it has no limit.</summary>
    public int? MaxSize { get; }
    private int _currentIndex = -1;

    /// <summary>
    /// Occurs when the history stack state or current index changes.
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>
    /// Gets the total number of entries currently in the history stack.
    /// </summary>
    public int Count => _entries.Count;

    /// <summary>
    /// Gets the index of the currently active entry in the history stack.
    /// </summary>
    public int CurrentIndex => _currentIndex;

    /// <summary>
    /// Gets a value indicating whether routing can move backward in history.
    /// </summary>
    public bool CanGoBack => _currentIndex > 0;

    /// <summary>
    /// Gets a value indicating whether routing can move forward in history.
    /// </summary>
    public bool CanGoForward => _currentIndex >= 0 && _currentIndex < _entries.Count - 1;

    /// <summary>
    /// Gets the currently active history entry, or <see langword="null"/> if the history is empty.
    /// </summary>
    public RouterEntry? Current => _currentIndex < 0 ? null : _entries[_currentIndex];

    /// <summary>
    /// Gets the root (first) entry in the history stack, or <see langword="null"/> if the history is empty.
    /// </summary>
    public RouterEntry? Root => _entries.Count > 0 ? _entries[0] : null;

    IHistoryEntry? IRouterHistory.Current => Current;
    IHistoryEntry? IRouterHistory.Root => Root;
    IReadOnlyList<IHistoryEntry> IRouterHistory.Entries => _entries.ToArray();   // snapshot

    /// <summary>
    /// Pushes a new entry onto the history stack, discarding any forward history and, beyond <see cref="MaxSize"/>, the oldest entry.
    /// </summary>
    /// <param name="entry">The router entry to push.</param>
    /// <returns>A list of entries that were removed (the forward entries, and the evicted oldest one) as a result of the push operation.</returns>
    public IReadOnlyList<RouterEntry> Push(RouterEntry entry)
    {
        var removed = new List<RouterEntry>();

        if (_currentIndex >= 0 && _currentIndex < _entries.Count - 1)
        {
            var count = _entries.Count - _currentIndex - 1;
            removed.AddRange(_entries.GetRange(_currentIndex + 1, count));
            _entries.RemoveRange(_currentIndex + 1, count);
        }

        _entries.Add(entry);
        if (MaxSize is { } max && _entries.Count > max)
        {
            removed.Add(_entries[0]);
            _entries.RemoveAt(0);
        }

        _currentIndex = _entries.Count - 1;
        OnChanged();
        return removed;
    }

    /// <summary>
    /// Pops the current entry and all subsequent forward entries from the stack, moving back one level.
    /// </summary>
    /// <returns>A list of entries that were removed during the pop operation.</returns>
    public IReadOnlyList<RouterEntry> Pop()
    {
        if (!CanGoBack) return [];
        var removed = _entries.GetRange(_currentIndex, _entries.Count - _currentIndex);
        _entries.RemoveRange(_currentIndex, removed.Count);
        _currentIndex--;
        OnChanged();
        return removed;
    }

    /// <summary>
    /// Clears all entries after the root entry, resetting the current index to the root.
    /// </summary>
    /// <returns>A list of entries that were removed.</returns>
    public IReadOnlyList<RouterEntry> MoveToRoot()
    {
        if (_entries.Count <= 1)
        {
            if (_entries.Count == 1) _currentIndex = 0;
            return [];
        }
        var removed = _entries.GetRange(1, _entries.Count - 1);
        _entries.RemoveRange(1, removed.Count);
        _currentIndex = 0;
        OnChanged();
        return removed;
    }

    /// <summary>
    /// Clears the entire history stack and resets the current index.
    /// </summary>
    /// <returns>A list containing all entries that were removed.</returns>
    public IReadOnlyList<RouterEntry> Clear()
    {
        var removed = _entries.ToList();
        _entries.Clear();
        _currentIndex = -1;
        OnChanged();
        return removed;
    }

    /// <summary>
    /// Creates an array snapshot of all current history entries.
    /// </summary>
    /// <returns>An array representing the current history state.</returns>
    internal RouterEntry[] Snapshot() => _entries.ToArray();

    /// <summary>
    /// Moves the active index one step backward in the history stack.
    /// </summary>
    /// <returns>The new current router entry, or <see langword="null"/> if moving back is not possible.</returns>
    public RouterEntry? MoveBack()
    {
        if (!CanGoBack) return null;
        _currentIndex--;
        OnChanged();
        return _entries[_currentIndex];
    }

    /// <summary>
    /// Moves the active index one step forward in the history stack.
    /// </summary>
    /// <returns>The new current router entry, or <see langword="null"/> if moving forward is not possible.</returns>
    public RouterEntry? MoveForward()
    {
        if (!CanGoForward) return null;
        _currentIndex++;
        OnChanged();
        return _entries[_currentIndex];
    }

    /// <summary>
    /// Replaces the currently active history entry with a new one.
    /// </summary>
    /// <param name="entry">The new router entry.</param>
    public void ReplaceCurrent(RouterEntry entry)
    {
        if (_currentIndex < 0)
        {
            Push(entry);
            return;
        }
        _entries[_currentIndex] = entry;
        OnChanged();
    }

    /// <summary>
    /// Peeks at the previous entry without changing the current route change index.
    /// </summary>
    internal RouterEntry? PeekBack() => CanGoBack ? _entries[_currentIndex - 1] : null;

    /// <summary>
    /// Peeks at the next entry without changing the current route change index.
    /// </summary>
    internal RouterEntry? PeekForward() => CanGoForward ? _entries[_currentIndex + 1] : null;

    /// <summary>
    /// Invokes the <see cref="Changed"/> event.
    /// </summary>
    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Retrieves the entry at the specified index, or <see langword="null"/> if the index is out of bounds.
    /// </summary>
    /// <param name="index">The zero-based index of the entry.</param>
    internal RouterEntry? At(int index)
        => index >= 0 && index < _entries.Count ? _entries[index] : null;

    /// <summary>
    /// Moves the current index directly to the specified target index.
    /// </summary>
    /// <param name="index">The target index to go to within history.</param>
    public void MoveTo(int index)
    {
        if (index < 0 || index >= _entries.Count || index == _currentIndex) return;
        _currentIndex = index;
        OnChanged();
    }
}
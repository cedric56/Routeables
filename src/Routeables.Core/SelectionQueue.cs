namespace Routeables.Core;

/// <summary>
/// The selections of a menu: they run one at a time, each has a version, and a newer one replaces the ones before it (a selection that a newer one
/// has replaced does nothing).
/// </summary>
internal sealed class SelectionQueue
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private int _version;

    // The versions of the selections that are running or waiting for the gate. When the newest is cancelled, the version that becomes current
    // again is the newest of these, not simply the one before it (which may have been cancelled too).
    private readonly SortedSet<int> _live = [];

    /// <summary>The version of the newest selection.</summary>
    public int Version => Volatile.Read(ref _version);

    /// <summary>Registers a selection, which becomes the newest, and gives its version.</summary>
    public int Begin()
    {
        var version = Interlocked.Increment(ref _version);
        lock (_live)
            _live.Add(version);
        return version;
    }

    /// <summary>Waits until the selections before this one are over.</summary>
    public Task WaitAsync(CancellationToken cancellationToken) => _gate.WaitAsync(cancellationToken);

    /// <summary>
    /// A selection was cancelled while it waited, so it never ran: forgets it.
    /// </summary>
    /// <returns><see langword="true"/> when it was still the newest, and the newest of the selections that remain is now the current version.</returns>
    public bool Abandon(int version)
    {
        int newest;
        lock (_live)
        {
            _live.Remove(version);
            newest = _live.Count > 0 ? _live.Max : version - 1;
        }

        return Interlocked.CompareExchange(ref _version, newest, version) == version;
    }

    /// <summary>A selection that held the gate is over: forgets it and lets the next one run.</summary>
    public void Finish(int version)
    {
        lock (_live)
            _live.Remove(version);
        _gate.Release();
    }
}

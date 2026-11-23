using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Abstraction.Errors;
using Routeables.Abstraction.Routing;

namespace Routeables.Services;

/// <summary>
/// Keeps the history of the router between two runs of the application, on every host that has a container of its own (WPF, Avalonia, WinUI,
/// Uno, WinUI 3, MAUI): the pages shown, with what they saved (<see cref="RouteableState"/>), are written to an
/// <see cref="IRouteableHistoryStore"/> after each route change and put back when the application starts again, once its first page is shown.
/// Switched on by <see cref="RouteableHistoryExtensions.AddRouteableHistory(IServiceCollection, string?)"/>; the host starts it when it builds its container.
/// </summary>
/// <remarks>
/// Blazor keeps the history of a circuit with <c>PersistentComponentState</c> instead (see <c>RouteableHistoryOptions</c>), and the ASP.NET Core
/// host keeps a scope per visitor in memory.
/// </remarks>
public sealed class RouteableHistoryKeeper(IServiceProvider provider, IRouteableHistoryStore store, IRouteableHistoryProtector? protector) : IDisposable
{
    /// <summary>Creates the keeper with a history that is not protected (the constructor of version 2.1.0).</summary>
    /// <param name="provider">The container of the application.</param>
    /// <param name="store">Where the history is kept.</param>
    public RouteableHistoryKeeper(IServiceProvider provider, IRouteableHistoryStore store) : this(provider, store, null)
    {
    }

    private readonly object _gate = new();
    private IRouter? _router;
    private volatile bool _active;
    private volatile bool _disposed;
    private readonly CancellationTokenSource _stopped = new();
    private string? _last;
    private long _ticket; // taken before a history is read
    private long _savedTicket; // the ticket of the history written last

    /// <summary>Starts keeping the history: reads what was kept, and writes the history after each route change from then on. Safe to call twice.</summary>
    public void Start()
    {
        IRouter? router;
        lock (_gate)
        {
            if (_router is not null || provider.GetService<IRouter>() is not { } found)
                return;
            _router = router = found;
        }

        IReadOnlyList<RouteableSnapshot> kept = [];
        try { kept = RouteableState.DeserializeSnapshots(Unprotect(store.Load())); }
        catch (Exception ex) { RouteableErrors.Report<RouteableHistoryKeeper>(provider, ex, "Load"); }

        // Subscribing and Dispose share the lock: either Dispose runs first and nothing is subscribed, or it runs after and unsubscribes.
        lock (_gate)
        {
            if (_disposed)
                return;
            router.Routed += OnRouted;
            router.History.Changed += OnHistoryChanged;

            // Write nothing before the pages are back: the first page of the new run must not replace the history that is waiting.
            if (kept.Count == 0)
            {
                _active = true;
                return;
            }
        }

        // The addresses that opened the application (RouteableLinkInbox) wait until the pages are back: restoring starts by erasing the history.
        RouteableStartup.BeginRestore(router);
        RouteableHelper.FireAndForget(() => Task.Run(() => RestoreAsync(router, kept)), provider, "RouteableHistoryKeeper.Restore");
    }

    // How long one wait for the first page lasts. The wait restarts until the keeper is disposed. Changed by tests only.
    internal static TimeSpan FirstPageSlice = TimeSpan.FromSeconds(30);

    private async Task RestoreAsync(IRouter router, IReadOnlyList<RouteableSnapshot> kept)
    {
        // The keeper may be disposed before this runs: its source is disposed with it, and a disposed source gives no token.
        CancellationToken stop;
        try { stop = _stopped.Token; }
        catch (ObjectDisposedException)
        {
            // The restore was announced (BeginRestore): the addresses waiting for it must not wait forever.
            RouteableStartup.EndRestore(router);
            return;
        }

        try
        {
            await PutBackWhenShownAsync(router, kept, stop);
        }
        catch (Exception ex)
        {
            RouteableErrors.Report<RouteableHistoryKeeper>(provider, ex, "Restore");
        }
        finally
        {
            await FinishRestoreAsync(router);
        }
    }

    // Puts the pages back once the first page is shown, however late that is (a sign-in window, a splash screen outside the router).
    // Giving up after a delay would let the next navigation overwrite the history with a single page.
    private async Task PutBackWhenShownAsync(IRouter router, IReadOnlyList<RouteableSnapshot> kept, CancellationToken stop)
    {
        while (!await RouteableLinkInbox.WaitForFirstPageAsync(router, stop, FirstPageSlice))
        {
            if (_disposed || stop.IsCancellationRequested)
                return;
        }

        if (_disposed)
            return;

        async Task PutBack()
        {
            if (await RouteableState.RestoreSnapshotsAsync(router, kept))
                RouteableMenu.Moved(provider, router);
            else
                SaveNow(); // what was kept cannot be shown: the page that is there replaces it, or the same failure would come back at every start
        }

        if (provider.GetService<IRouteableDispatcher>() is { } dispatcher && !dispatcher.CheckAccess())
            await dispatcher.InvokeAsync(PutBack);
        else
            await PutBack();
    }

    // However the restore ended: the keeper writes again (unless it was disposed), the addresses that waited for the restore go on, and the history
    // as it is now is written.
    private async Task FinishRestoreAsync(IRouter router)
    {
        // A keeper disposed while it restored stays off: it must not write the history again.
        // Under the lock of Dispose: a Dispose that runs between a test and the assignment would be undone.
        lock (_gate)
        {
            if (!_disposed)
                _active = true;
        }
        RouteableStartup.EndRestore(router);
        try
        {
            // The history belongs to the UI thread: read it there, as the navigations that save it do.
            if (_disposed)
            {
                // A keeper that is over writes nothing.
            }
            else if (provider.GetService<IRouteableDispatcher>() is { } dispatcher && !dispatcher.CheckAccess())
                await dispatcher.InvokeAsync(SaveNow);
            else
                SaveNow();
        }
        catch (Exception ex)
        {
            RouteableErrors.Report<RouteableHistoryKeeper>(provider, ex, "Save");
        }
    }

    private void OnRouted(object? sender, RouteableRoutedEventArgs e)
    {
        if (_active && e.Succeeded)
            SaveNow();
    }

    // An erased history raises no navigation: without this, the erased pages would come back at the next start.
    private void OnHistoryChanged(object? sender, EventArgs e)
    {
        if (_active && _router is { } router && router.History.Count == 0)
            SaveNow(allowEmpty: true);
    }

    /// <summary>Writes the history now, without waiting for the next route change (the application is going to sleep). Does nothing before <see cref="Start"/> has put the previous history back.</summary>
    public void Flush()
    {
        if (!_active)
            return;

        // The history belongs to the UI thread (a view model saves its state there): from another thread, the work goes through the dispatcher,
        // as the restore does. The wait is short: a thread that is blocked on the UI thread must not hang the application when it goes to sleep.
        if (provider.GetService<IRouteableDispatcher>() is { } dispatcher && !dispatcher.CheckAccess())
        {
            try
            {
                if (!dispatcher.InvokeAsync(SaveNow).Wait(FlushWait))
                    RouteableErrors.Report<RouteableHistoryKeeper>(provider, new TimeoutException("The interface thread did not save the history in time."), "Flush");
            }
            catch (Exception ex)
            {
                RouteableErrors.Report<RouteableHistoryKeeper>(provider, ex, "Flush");
            }

            return;
        }

        SaveNow();
    }

    // How long Flush waits for the interface thread. Changed by tests only.
    internal static TimeSpan FlushWait = TimeSpan.FromSeconds(3);

    // A history that cannot be unprotected (another key, a modified file) is left out: the application starts with no history.
    private string? Unprotect(string? kept)
    {
        if (protector is null || string.IsNullOrEmpty(kept))
            return kept;
        return protector.Unprotect(kept);
    }

    private void SaveNow() => SaveNow(allowEmpty: false);

    private void SaveNow(bool allowEmpty)
    {
        try
        {
            if (_router is not { } router)
                return;

            // Two saves can overlap when no dispatcher is registered (with one, Flush runs on the UI thread like the navigations, so they follow
            // one another). The save that read the history last must be written last. The history is read outside the lock (a view model saves
            // its state there, which is user code), so each read first takes a number, and a read older than what is already written is dropped.
            var ticket = Interlocked.Increment(ref _ticket);
            var snapshots = RouteableState.CaptureSnapshots(router);
            // An empty history is written only when it was erased: the first page of a new run is not there yet when the keeper starts.
            if (snapshots.Count == 0 && !allowEmpty)
                return;

            var text = snapshots.Count == 0 ? string.Empty : RouteableState.Serialize(snapshots);
            lock (_gate)
            {
                if (ticket < _savedTicket)
                    return;
                // Nothing to write, but this read is now the newest one looked at: an older, different read must not be written after it.
                if (text == _last)
                {
                    _savedTicket = ticket;
                    return;
                }
                store.Save(protector is null || text.Length == 0 ? text : protector.Protect(text));
                _last = text;
                _savedTicket = ticket;
            }
        }
        catch (Exception ex)
        {
            // A history that cannot be written (a read-only disk) must not make a navigation fail.
            RouteableErrors.Report<RouteableHistoryKeeper>(provider, ex, "Save");
        }
    }

    /// <summary>Stops keeping the history.</summary>
    public void Dispose()
    {
        IRouter? router;
        lock (_gate)
        {
            _disposed = true;
            _active = false;
            router = _router;
            if (router is not null)
            {
                router.Routed -= OnRouted;
                router.History.Changed -= OnHistoryChanged;
            }
        }

        try
        {
            _stopped.Cancel();
            _stopped.Dispose();
        }
        catch (ObjectDisposedException) { } // already stopped
    }
}

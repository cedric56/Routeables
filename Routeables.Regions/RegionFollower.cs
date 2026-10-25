using Routeables.Abstraction;

namespace Routeables.Regions;

/// <summary>
/// Makes other regions follow one: each time the history of the <em>source</em> router changes, <c>update</c> runs, one run
/// after the other, never overlapping and never on the call that routed.
/// <para>
/// The history of a router changes while the router holds its gate, so a handler of
/// <see cref="IRouterHistory.Changed"/> must not route that router. The follower is the place to route the
/// <em>other</em> regions (a header that says what the content shows, a menu that selects its entry): it queues the work.
/// </para>
/// <para>
/// The runs go back to the thread that created the follower (the UI thread when it is created from a view model that a view
/// started), because the views of the regions are built by their routers and a control cannot be created on another thread.
/// Pass a <c>scheduler</c> to choose another one.
/// </para>
/// </summary>
public sealed class RegionFollower : IDisposable
{
    private readonly IRouter _source;
    private readonly Func<Task> _update;
    private readonly TaskScheduler _scheduler;
    private readonly object _lock = new();
    private Task _queue = Task.CompletedTask;
    private bool _disposed;

    /// <summary>Runs <paramref name="update"/> each time the history of <paramref name="source"/> changes, one run after the other.</summary>
    /// <param name="source">The router to follow.</param>
    /// <param name="update">What to do after a change.</param>
    /// <param name="scheduler">Where <paramref name="update"/> runs; the current scheduler when <see langword="null"/>.</param>
    public RegionFollower(IRouter source, Func<Task> update, TaskScheduler? scheduler = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(update);
        _source = source;
        _update = update;
        _scheduler = scheduler
            ?? (SynchronizationContext.Current is null ? TaskScheduler.Default : TaskScheduler.FromCurrentSynchronizationContext());
        source.History.Changed += OnChanged;
    }

    /// <summary>Raised when an update failed: a region that cannot follow must not break the one it follows.</summary>
    public event EventHandler<Exception>? Failed;

    /// <summary>Completes when every update queued so far has run.</summary>
    public Task SettledAsync()
    {
        lock (_lock)
            return _queue;
    }

    /// <summary>Queues an update without a change of the source (the first one, for instance).</summary>
    public void Update()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _queue = _queue.ContinueWith(_ => RunAsync(), CancellationToken.None, TaskContinuationOptions.None, _scheduler).Unwrap();
        }
    }

    private void OnChanged(object? sender, EventArgs e) => Update();

    private async Task RunAsync()
    {
        // An update queued before Dispose does not run after it.
        lock (_lock)
            if (_disposed) return;

        try
        {
            await _update();
        }
        catch (Exception ex)
        {
            // Told to whoever follows the failures. With nobody listening (or a listener that throws) it is reported instead of lost.
            if (Failed is not { } failed)
            {
                RouteableErrors.Report<RegionFollower>(null, ex);
                return;
            }

            try { failed(this, ex); }
            catch (Exception handler) { RouteableErrors.Report<RegionFollower>(null, handler, nameof(Failed)); }
        }
    }

    /// <summary>Stops following: updates already queued do not run.</summary>
    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
        }

        _source.History.Changed -= OnChanged;
    }
}

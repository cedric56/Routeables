using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Abstraction.Errors;
using Routeables.Abstraction.Routing;

namespace Routeables.Services;

/// <summary>
/// Where the addresses opened from outside and the pages saved by the system wait for the application, and where they are
/// delivered once the first page is shown. A host receives them in its own way (an Android intent, an iOS URL, a command-line argument)
/// and calls <see cref="Push"/> and <see cref="Restore"/>; the application calls <see cref="Attach"/> after the container is built.
/// </summary>
/// <remarks>
/// The delivery runs on the thread of the interface (<see cref="IRouteableDispatcher"/>, when the host registers one), one step at a time:
/// first the saved pages, then the addresses in the order they came. Each address goes through <see cref="RouteableDeepLinks"/>, so the
/// guards apply and an address that cannot be followed is skipped.
/// </remarks>
public sealed class RouteableLinkInbox
{
    private static readonly TimeSpan FirstPageTimeout = TimeSpan.FromSeconds(30);

    // How long one wait for the first page lasts. A page that comes later still restarts the pump. Changed by tests only.
    internal TimeSpan FirstPageWait { get; init; } = FirstPageTimeout;

    /// <summary>The most addresses kept while the application is not ready; beyond it the oldest are dropped (the last address is the one the user wants).</summary>
    internal const int MaxPending = 32;

    private readonly IReadOnlyCollection<string>? schemes;
    private readonly IReadOnlyCollection<string>? hosts;
    private IServiceProvider? provider;
    private readonly object gate = new();
    private readonly Queue<Uri> links = new();
    private IReadOnlyList<RouteableSnapshot>? saved;
    private bool started;
    private bool pumping;

    /// <summary>
    /// Creates an inbox before the container exists (a static field of the application, which the Android activity fills at start).
    /// What it receives waits for <see cref="Attach"/>.
    /// </summary>
    /// <param name="schemes">The schemes that are accepted (<c>"myapp"</c>, <c>"https"</c>); <see langword="null"/> or empty accepts no scheme of the application: give the ones it owns.</param>
    /// <param name="hosts">The hosts of the <c>http</c> and <c>https</c> links that are accepted; <see langword="null"/> or empty accepts no web link: set it when <c>https</c> is accepted.</param>
    public RouteableLinkInbox(IReadOnlyCollection<string>? schemes = null, IReadOnlyCollection<string>? hosts = null)
    {
        this.schemes = schemes;
        this.hosts = hosts;
    }

    /// <summary>Creates the inbox for a container and starts it.</summary>
    /// <param name="provider">The container of the host.</param>
    /// <param name="schemes">The schemes that are accepted; <see langword="null"/> or empty accepts no scheme of the application: give the ones it owns.</param>
    /// <param name="hosts">The hosts of the <c>http</c> and <c>https</c> links that are accepted; <see langword="null"/> or empty accepts no web link.</param>
    public RouteableLinkInbox(IServiceProvider provider, IReadOnlyCollection<string>? schemes = null, IReadOnlyCollection<string>? hosts = null) : this(schemes, hosts)
    {
        ArgumentNullException.ThrowIfNull(provider);
        Attach(provider);
    }

    /// <summary>
    /// Gives the container and starts the delivery: what was pushed before is delivered as soon as the first page is shown.
    /// Call it once the container is built.
    /// </summary>
    public void Attach(IServiceProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        lock (gate)
        {
            this.provider = provider;
            started = true;
        }

        Kick();
    }

    /// <summary>The addresses that wait to be followed (for tests).</summary>
    internal int PendingCount { get { lock (gate) return links.Count; } }

    /// <summary>An address received by the host. Safe to call from any thread, before or after <see cref="Attach"/>.</summary>
    public void Push(Uri? uri)
    {
        // An address that would be refused when followed (see RouteableDeepLinks.MaxLength) is not worth keeping, and outside input must not be
        // able to fill the memory while the first page is not shown.
        if (uri is null || uri.OriginalString.Length > RouteableDeepLinks.MaxLength)
            return;

        lock (gate)
        {
            links.Enqueue(uri);
            while (links.Count > MaxPending)
                links.Dequeue();
        }

        Kick();
    }

    /// <summary>The pages saved by <see cref="Save"/>, put back before the addresses are followed.</summary>
    public void Restore(string? state)
    {
        var snapshots = RouteableState.DeserializeSnapshots(state);
        if (snapshots.Count == 0)
            return;

        lock (gate)
            saved = snapshots;
        Kick();
    }

    /// <summary>The pages shown now, with their values, as one string for the host to keep (an Android bundle, a setting).</summary>
    public string? Save()
        => provider?.GetService<IRouter>() is { } router ? RouteableState.Serialize(RouteableState.CaptureSnapshots(router)) : null;

    private void Kick()
    {
        lock (gate)
        {
            if (!started || pumping)
                return;
            pumping = true;
        }

        // Run without the caller's execution context: Kick may run inside a navigation (a Routed handler), and the pump would carry the token of
        // the navigation lock still held, so its own navigation would look reentrant and the link would be lost.
        using (ExecutionContext.SuppressFlow())
            RouteableHelper.FireAndForget(() => Task.Run(PumpAsync), provider, "RouteableLinkInbox.PumpAsync");
    }

    private async Task PumpAsync()
    {
        var noFirstPage = false;
        IRouter? waitingFor = null;
        try
        {
            var services = provider!;
            if (services.GetService<IRouter>() is not { } router)
            {
                // No router is registered (a configuration mistake): starting again would spin forever, as for a page that never comes.
                noFirstPage = true;
                return;
            }
            if (!await WaitForFirstPageAsync(router, timeout: FirstPageWait))
            {
                // No page after the wait: the links stay. Starting again here would poll forever in an application that never shows a page. The first page
                // that is shown (a sign-in window can take longer than the wait) restarts the pump, and so does the next link.
                noFirstPage = true;
                waitingFor = router;
                return;
            }

            // Restore the last run's history first, when there is one: it starts by erasing the history, and would remove a link's page.
            await RouteableStartup.WhenRestoredAsync(router);

            while (true)
            {
                IReadOnlyList<RouteableSnapshot>? restoring;
                Uri? uri = null;
                lock (gate)
                {
                    restoring = saved;
                    saved = null;
                    if (restoring is null && links.Count > 0)
                        uri = links.Dequeue();
                }

                if (restoring is null && uri is null)
                    return;

                // A link tells the menu itself (RouteableDeepLinks); restored pages do it here.
                await OnInterfaceAsync(restoring is not null
                    ? async () =>
                    {
                        if (await RouteableState.RestoreSnapshotsAsync(router, restoring))
                            RouteableMenu.Moved(services, router);
                    }
                : async () => await RouteableDeepLinks.GoToAsync(services, uri!, schemes, hosts: hosts));
            }
        }
        catch (Exception ex)
        {
            if (provider is { } failed)
                RouteableErrors.Report(failed, ex, typeof(RouteableLinkInbox));
        }
        finally
        {
            bool again;
            lock (gate)
            {
                pumping = false;
                again = saved is not null || links.Count > 0;
            }

            if (again && !noFirstPage)
                Kick();
            else if (again && waitingFor is not null)
                KickAtFirstPage(waitingFor);
        }
    }

    private bool watching;

    // Start the pump when the first page is shown, however late: the link that opened the application is then followed, not dropped.
    private void KickAtFirstPage(IRouter router)
    {
        lock (gate)
        {
            if (watching)
                return;
            watching = true;
        }

        void OnRouted(object? sender, RouteableRoutedEventArgs e)
        {
            if (router.History.Count == 0)
                return;
            router.Routed -= OnRouted;
            lock (gate)
                watching = false;
            Kick();
        }

        router.Routed += OnRouted;
        // The page may have been shown between the end of the wait and the subscription.
        if (router.History.Count > 0)
            OnRouted(router, null!);
    }

    // The menu selects its first entry when the window opens; before that the history is empty.
    internal static async Task<bool> WaitForFirstPageAsync(IRouter router, CancellationToken cancellationToken = default, TimeSpan? timeout = null)
    {
        // A monotonic clock: a change of the system time must not shorten or lengthen the wait.
        var waited = System.Diagnostics.Stopwatch.StartNew();
        var limit = timeout ?? FirstPageTimeout;
        while (router.History.Count == 0)
        {
            if (waited.Elapsed > limit || cancellationToken.IsCancellationRequested)
                return false;
            try { await Task.Delay(50, cancellationToken); }
            catch (OperationCanceledException) { return false; }
        }

        return true;
    }

    private async Task OnInterfaceAsync(Func<Task> work)
    {
        if (provider!.GetService<IRouteableDispatcher>() is { } dispatcher && !dispatcher.CheckAccess())
            await dispatcher.InvokeAsync(work);
        else
            await work();
    }
}

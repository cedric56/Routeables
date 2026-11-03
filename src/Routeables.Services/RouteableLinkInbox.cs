using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;

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

    private readonly IReadOnlyCollection<string>? schemes;
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
    /// <param name="schemes">The schemes that are accepted (<c>"myapp"</c>, <c>"https"</c>); <see langword="null"/> or empty accepts any.</param>
    public RouteableLinkInbox(IReadOnlyCollection<string>? schemes = null) => this.schemes = schemes;

    /// <summary>Creates the inbox for a container and starts it.</summary>
    /// <param name="provider">The container of the host.</param>
    /// <param name="schemes">The schemes that are accepted; <see langword="null"/> or empty accepts any.</param>
    public RouteableLinkInbox(IServiceProvider provider, IReadOnlyCollection<string>? schemes = null) : this(schemes)
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

    /// <summary>An address received by the host. Safe to call from any thread, before or after <see cref="Attach"/>.</summary>
    public void Push(Uri? uri)
    {
        if (uri is null)
            return;

        lock (gate)
            links.Enqueue(uri);
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

        _ = Task.Run(PumpAsync);
    }

    private async Task PumpAsync()
    {
        try
        {
            var services = provider!;
            if (services.GetService<IRouter>() is not { } router || !await WaitForFirstPageAsync(router))
                return;

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

                // A link tells the menu itself (RouteableDeepLinks); the pages that are put back do it here.
                await OnInterfaceAsync(restoring is not null
                    ? async () =>
                    {
                        if (await RouteableState.RestoreSnapshotsAsync(router, restoring))
                            RouteableMenu.Moved(services, router);
                    }
                    : async () => await RouteableDeepLinks.GoToAsync(services, uri!, schemes));
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

            if (again)
                Kick();
        }
    }

    // The menu selects its first entry when the window opens: before that the history is empty.
    private static async Task<bool> WaitForFirstPageAsync(IRouter router)
    {
        var until = DateTime.UtcNow + FirstPageTimeout;
        while (router.History.Count == 0)
        {
            if (DateTime.UtcNow > until)
                return false;
            await Task.Delay(50);
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

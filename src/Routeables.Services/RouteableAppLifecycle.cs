using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction.Errors;
using Routeables.Abstraction.Lifecycle;
using Routeables.Abstraction.Routing;

namespace Routeables.Services;

/// <summary>
/// Tells the pages that are shown (main router and regions) that the application goes to sleep or comes back (<see cref="IRouteableAppLifecycle"/>),
/// and saves the history after a sleep, so that the <c>[Persist]</c> properties written by <c>OnSleepAsync</c> are kept. The hosts that have an
/// application lifecycle call it; a host that has none can call it from its own event.
/// </summary>
public sealed class RouteableAppLifecycle(IServiceProvider provider)
{
    /// <summary>The application goes to the background or closes: the shown pages are told, then the history is saved.</summary>
    public Task SleepAsync(CancellationToken cancellationToken = default) => OnUiThreadAsync(async () =>
    {
        await NotifyAsync(static (target, ct) => target.OnSleepAsync(ct), cancellationToken);
        try { provider.GetService<RouteableHistoryKeeper>()?.Flush(); }
        catch (Exception ex) { RouteableErrors.Report<RouteableAppLifecycle>(provider, ex, "Flush"); }
    });

    /// <summary>The application is back: the shown pages are told.</summary>
    public Task ResumeAsync(CancellationToken cancellationToken = default) => OnUiThreadAsync(
        () => NotifyAsync(static (target, ct) => target.OnResumeAsync(ct), cancellationToken));

    /// <summary>
    /// <see cref="SleepAsync"/> for the moment a window closes, when nothing can be awaited any more. Returns <see langword="false"/> when the pages
    /// did not finish within <paramref name="timeout"/> (a page that awaits work resuming on the interface thread cannot finish while that thread waits here).
    /// </summary>
    public bool SleepNow(TimeSpan? timeout = null)
    {
        try { return SleepAsync().Wait(timeout ?? TimeSpan.FromSeconds(2)); }
        catch (Exception ex) { RouteableErrors.Report<RouteableAppLifecycle>(provider, ex, "SleepNow"); return false; }
    }

    private Task OnUiThreadAsync(Func<Task> work)
        => provider.GetService<IRouteableDispatcher>() is { } dispatcher && !dispatcher.CheckAccess()
            ? dispatcher.InvokeAsync(work)
            : work();

    private async Task NotifyAsync(Func<IRouteableAppLifecycle, CancellationToken, Task> call, CancellationToken ct)
    {
        var routers = new List<IRouter>();
        if (provider.GetService<IRouter>() is { } main) routers.Add(main);
        var regions = provider.GetService<RouteableRegionNames>()?.All ?? [];
        foreach (var name in regions)
            if (provider.GetKeyedService<IRouter>(name) is { } region) routers.Add(region);

        foreach (var router in routers)
        {
            // The page and its view model both may care; a page that throws must not stop the others being told.
            foreach (var shown in new[] { router.CurrentViewModel, router.CurrentView })
            {
                if (shown is not IRouteableAppLifecycle target) continue;
                try { await call(target, ct); }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex) { RouteableErrors.Report<RouteableAppLifecycle>(provider, ex, shown.GetType().Name); }
            }
        }
    }
}

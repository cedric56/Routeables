using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Routing;

namespace Routeables.Benchmarks;

/// <summary>
/// Not a timing: checks over many route_changes that the history grows by one entry for each, that the pages the router lets go (erased) can be
/// collected, and that disposable view models are disposed. Run with <c>--leak</c>; the exit code is 0 only if all pass.
/// </summary>
internal static class LeakCheck
{
    private const int RouteChanges = 5_000;

    public static async Task<int> Run()
    {
        var failures = 0;
        void Report(string name, bool ok, string detail)
        {
            Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {name}: {detail}");
            if (!ok) failures++;
        }

        // 1. The history has no limit: one entry for each route change, and none left once it is erased.
        using (var provider = Bench.Build(factories: true))
        {
            var router = provider.GetRequiredService<IRouter>();
            for (var i = 0; i < RouteChanges; i++)
                Bench.Ok(await router.GoToAsync(i % 2 == 0 ? "/a" : "/b"));
            var grown = router.History.Count;
            await router.EraseHistoryAsync();
            Report("history grows by one entry per route change, and erases", grown == RouteChanges && router.History.Count == 0,
                $"{grown} entries after {RouteChanges} route_changes, {router.History.Count} once erased");
        }

        // 2. View models dropped from the history can be collected.
        using (var provider = Bench.Build(factories: true))
        {
            var router = provider.GetRequiredService<IRouter>();
            var weak = await GoTo(router, RouteChanges, track: 200);
            await router.EraseHistoryAsync();   // the history keeps every page until it is erased
            Collect();
            var alive = weak.Count(w => w.IsAlive);
            Report("dropped view models are collectable", alive == 0, $"{alive} of the first {weak.Count} view models are still alive");
        }

        // 3. Disposable view models are disposed once they leave the history, and what the container keeps is reported.
        using (var provider = Bench.Build(factories: true))
        {
            var router = provider.GetRequiredService<IRouter>();
            VmDisposable.Created = VmDisposable.Disposed = 0;
            for (var i = 0; i < RouteChanges; i++)
                Bench.Ok(await router.GoToAsync(i % 2 == 0 ? "/disposable" : "/a"));
            await router.EraseHistoryAsync();
            Report("disposable view models are disposed", VmDisposable.Disposed == VmDisposable.Created,
                $"{VmDisposable.Disposed} disposed of {VmDisposable.Created} created");
        }

        // 4. The managed heap does not grow with the number of route_changes.
        using (var provider = Bench.Build(factories: true))
        {
            var router = provider.GetRequiredService<IRouter>();
            await Churn(router, 1_000);
            await router.EraseHistoryAsync();
            Collect();
            var before = GC.GetTotalMemory(true);
            await Churn(router, RouteChanges);
            await router.EraseHistoryAsync();   // the history keeps every page until it is erased
            Collect();
            var growth = GC.GetTotalMemory(true) - before;
            Report("heap does not grow", growth < 1_000_000, $"{growth / 1024.0:F0} KiB more after {RouteChanges} route_changes");
        }

        Console.WriteLine(failures == 0 ? "All checks passed." : $"{failures} check(s) failed.");
        return failures == 0 ? 0 : 1;
    }

    /// <summary>Routes and returns weak references to the first <paramref name="track"/> view models. Kept apart so that no local of the caller keeps one alive.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<List<WeakReference>> GoTo(IRouter router, int count, int track)
    {
        var weak = new List<WeakReference>();
        for (var i = 0; i < count; i++)
        {
            Bench.Ok(await router.GoToAsync(i % 2 == 0 ? "/a" : "/b"));
            if (i < track) weak.Add(new WeakReference(router.CurrentViewModel));
        }
        return weak;
    }

    private static async Task Churn(IRouter router, int count)
    {
        for (var i = 0; i < count; i++)
            Bench.Ok(await router.GoToAsync(i % 3 == 0 ? "/a" : i % 3 == 1 ? "/b" : "/dep"));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Collect()
    {
        for (var i = 0; i < 3; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }
}

using Routeables.Abstraction;
using Routeables.Routing;
using Routeables.Services;

namespace Routeables.Tests.Services;

/// <summary>
/// The history keeper under random moves (pushes, back, forward, pop, root, a refused route): what is in the store is always the history that
/// is shown, and a new run of the application gets that history back.
/// </summary>
public class RouteableHistoryRandomizedTests
{
    public sealed class RandomPageView : IRouteableFor { public object? Context { get; set; } }
    public sealed class RandomPageVm;

    private sealed class MemoryStore : IRouteableHistoryStore
    {
        public string? Kept { get; set; }
        public string? Load() => Kept;
        public void Save(string text) => Kept = text;
    }

    private static readonly string[] Routes = ["/r0", "/r1", "/r2", "/r3", "/r4"];

    private static ServiceProvider Build(IRouteableHistoryStore store, int? limit)
    {
        var services = new ServiceCollection();
        services.RegisterRouting();
        foreach (var route in Routes)
            services.RegisterRoute<RandomPageView, RandomPageVm>(ServiceLifetime.Transient, ServiceLifetime.Transient, route);
        services.AddTransient<RandomPageVm>();
        if (limit is { } max)
            services.LimitHistory(max);
        services.AddSingleton(store);
        services.AddRouteableHistory();
        return services.BuildServiceProvider();
    }

    private static async Task UntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("The condition was not met in 5 seconds.");
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task The_store_always_holds_the_history_that_is_shown_and_the_next_run_gets_it_back()
    {
        var random = new Random(2024);
        var bad = new List<string>();

        for (var run = 0; run < 60; run++)
        {
            int? limit = random.Next(3) == 0 ? random.Next(2, 5) : null;
            var store = new MemoryStore();
            string trace = "";

            using (var provider = Build(store, limit))
            {
                provider.StartRouteableHistory();
                var router = provider.GetRequiredService<IRouter>();

                for (var step = 0; step < 40 && bad.Count == 0; step++)
                {
                    var op = random.Next(8);
                    trace += op;
                    switch (op)
                    {
                        case 0: case 1: case 2: await router.GoToAsync(Routes[random.Next(Routes.Length)], cancellationToken:TestContext.Current.CancellationToken); break;
                        case 3: await router.GoToAsync("/unknown-" + step, cancellationToken: TestContext.Current.CancellationToken); break;           // refused: nothing changes
                        case 4: await router.GoBackAsync(cancellationToken: TestContext.Current.CancellationToken); break;
                        case 5: await router.ForwardAsync(cancellationToken: TestContext.Current.CancellationToken); break;
                        case 6: await router.PopAsync(cancellationToken: TestContext.Current.CancellationToken); break;
                        default: await router.GoToAsync(Routes[random.Next(Routes.Length)], cancellationToken: TestContext.Current.CancellationToken); break;
                    }

                    var shown = RouteableState.Capture(router);
                    if (shown.Count == 0)
                        continue;

                    var kept = RouteableState.Deserialize(store.Kept);
                    if (!kept.SequenceEqual(shown))
                        bad.Add($"run {run} step {step} ops={trace} limit={limit}: shown [{string.Join(' ', shown)}] kept [{string.Join(' ', kept)}]");
                }

                if (bad.Count > 0)
                    break;
            }

            // The next run: its first page is shown, then the history comes back.
            var expected = RouteableState.Deserialize(store.Kept);
            if (expected.Count < 2)
                continue;

            using var next = Build(store, limit);
            next.StartRouteableHistory();
            var nextRouter = next.GetRequiredService<IRouter>();
            await nextRouter.GoToAsync("/r0", cancellationToken: TestContext.Current.CancellationToken);

            await UntilAsync(() => RouteableState.Capture(nextRouter).SequenceEqual(expected));
            if (!RouteableState.Deserialize(store.Kept).SequenceEqual(expected))
                bad.Add($"run {run} ops={trace}: the store changed while the history came back");
        }

        Assert.True(bad.Count == 0, string.Join("\n", bad.Take(5)));
    }

    // The whole road a user takes: browse, close the application, start it again, find the pages again, go back through them.
    [Fact]
    public async Task A_restart_with_the_file_store_brings_the_pages_back_and_Back_goes_through_them()
    {
        var folder = Path.Combine(Path.GetTempPath(), "routeables-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(folder, "history.txt");
        try
        {
            IServiceProvider Start(out IRouter router)
            {
                var services = new ServiceCollection();
                services.RegisterRouting();
                foreach (var route in Routes)
                    services.RegisterRoute<RandomPageView, RandomPageVm>(ServiceLifetime.Transient, ServiceLifetime.Transient, route);
                services.AddTransient<RandomPageVm>();
                services.AddRouteableHistory(path);
                var provider = services.BuildServiceProvider();
                provider.StartRouteableHistory();
                router = provider.GetRequiredService<IRouter>();
                return provider;
            }

            var ct = TestContext.Current.CancellationToken;
            var first = (ServiceProvider)Start(out var firstRouter);
            await firstRouter.GoToAsync("/r0", cancellationToken: ct);
            await firstRouter.GoToAsync("/r2", cancellationToken: ct);
            await firstRouter.GoToAsync("/r3", cancellationToken: ct);
            first.Dispose();                                    // the application is closed

            Assert.True(File.Exists(path));

            var second = (ServiceProvider)Start(out var secondRouter);
            await secondRouter.GoToAsync("/r0", cancellationToken: ct);   // the first page of the new run
            await UntilAsync(() => RouteableState.Capture(secondRouter).SequenceEqual(["/r0", "/r2", "/r3"]));

            await secondRouter.GoBackAsync(ct);
            Assert.Equal("/r2", secondRouter.History.Current!.Args.Route);
            await secondRouter.GoBackAsync(ct);
            Assert.Equal("/r0", secondRouter.History.Current!.Args.Route);
            Assert.False(secondRouter.CanGoBack);
            second.Dispose();
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task An_erased_history_is_not_brought_back_by_the_next_run()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = new MemoryStore();

        using (var provider = Build(store, limit: null))
        {
            provider.StartRouteableHistory();
            var router = provider.GetRequiredService<IRouter>();
            await router.GoToAsync("/r0", cancellationToken: ct);
            await router.GoToAsync("/r1", cancellationToken: ct);
            Assert.False(string.IsNullOrEmpty(store.Kept));

            await router.EraseHistoryAsync(ct);         // a sign-out: the pages must not come back
        }

        Assert.True(string.IsNullOrEmpty(store.Kept));

        using var next = Build(store, limit: null);
        next.StartRouteableHistory();
        var second = next.GetRequiredService<IRouter>();
        await second.GoToAsync("/r4", cancellationToken: ct);
        await Task.Delay(50, ct);
        Assert.Equal(["/r4"], RouteableState.Capture(second));
    }
}

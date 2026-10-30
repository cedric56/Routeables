using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Routing;

namespace Routeables.Benchmarks;

/// <summary>The history operations of one router, with a short history (2 entries) and with a long one (20 entries).</summary>
[MemoryDiagnoser]
public class RouteBenchmarks
{
    [Params(2, 20)]
    public int Depth { get; set; }

    private ServiceProvider provider = null!;
    private IRouter router = null!;
    private readonly IRouteableEntry viewB = new RouteableEntry<VmB>("B", null, "/b", "/b");

    [GlobalSetup]
    public async Task Setup()
    {
        provider = Bench.Build(factories: true);
        router = provider.GetRequiredService<IRouter>();
        await Bench.FillAsync(router, Depth);
    }

    [GlobalCleanup]
    public void Cleanup() => provider.Dispose();

    /// <summary>Route by address; with a full history every push also drops and disposes the oldest entry.</summary>
    [Benchmark(OperationsPerInvoke = 2)]
    public async Task GoTo_Address()
    {
        Bench.Ok(await router.GoToAsync("/a"));
        Bench.Ok(await router.GoToAsync("/b"));
    }

    /// <summary>Route with a view object (the typed path the generated methods use).</summary>
    [Benchmark(OperationsPerInvoke = 2)]
    public async Task GoTo_RouteableEntry()
    {
        Bench.Ok(await router.GoToAsync("/a"));
        Bench.Ok(await router.GoToRouteAsync(viewB));
    }

    /// <summary>Route to a page with a guard and a routing callback.</summary>
    [Benchmark(OperationsPerInvoke = 2)]
    public async Task GoTo_WithGuardAndCallback()
    {
        Bench.Ok(await router.GoToAsync("/a"));
        Bench.Ok(await router.GoToAsync("/guarded"));
    }

    /// <summary>Back then forward: no page is created, the entries are kept.</summary>
    [Benchmark(OperationsPerInvoke = 2)]
    public async Task Back_Forward()
    {
        if (!router.CanGoBack) return;
        Bench.Ok(await router.GoBackAsync());
        Bench.Ok(await router.ForwardAsync());
    }

    /// <summary>Push a page then leave it for good: the drill-down pattern.</summary>
    [Benchmark(OperationsPerInvoke = 2)]
    public async Task Push_Pop()
    {
        Bench.Ok(await router.GoToAsync("/b"));
        Bench.Ok(await router.PopAsync());
    }

    /// <summary>Jump to the oldest then the newest entry.</summary>
    [Benchmark(OperationsPerInvoke = 2)]
    public async Task MoveTo_FirstAndLast()
    {
        if (router.History.Count < 2) return;
        Bench.Ok(await router.MoveToAsync(0));
        Bench.Ok(await router.MoveToAsync(router.History.Count - 1));
    }

    /// <summary>Several route_changes requested at once on the same router: the gate runs them one after the other.</summary>
    [Benchmark]
    public async Task<int> Concurrent_SameRouter_8()
    {
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(i => router.GoToAsync(i % 2 == 0 ? "/a" : "/b")));
        return results.Count(r => r.Success);
    }
}

/// <summary>Filling a history with 20 pages, then emptying it: creation of 20 pages and the disposal of all of them.</summary>
[MemoryDiagnoser]
public class HistoryLifecycleBenchmarks
{
    private const int HistoryEntries = 20;

    private ServiceProvider provider = null!;
    private IRouter router = null!;

    [GlobalSetup]
    public void Setup()
    {
        provider = Bench.Build(factories: true);
        router = provider.GetRequiredService<IRouter>();
    }

    [GlobalCleanup]
    public void Cleanup() => provider.Dispose();

    [Benchmark]
    public async Task Fill20_ThenErase()
    {
        await Bench.FillAsync(router, HistoryEntries);
        await router.EraseHistoryAsync();
    }

    [Benchmark]
    public async Task Fill20_ThenBackToRoot()
    {
        await Bench.FillAsync(router, HistoryEntries);
        Bench.Ok(await router.GoBackToRootAsync());
        await router.EraseHistoryAsync();
    }
}

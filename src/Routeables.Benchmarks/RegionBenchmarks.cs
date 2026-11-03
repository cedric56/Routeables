using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;

namespace Routeables.Benchmarks;

/// <summary>
/// Independent routing areas. One invocation routes every region once (so it is <c>Regions</c> route_changes):
/// divide by <c>Regions</c> for the cost of one. If the cost per routing is constant, regions do not slow each other down.
/// </summary>
[MemoryDiagnoser]
public class RegionBenchmarks
{
    [Params(1, 4, 16)]
    public int Regions { get; set; }

    private ServiceProvider provider = null!;
    private IRouter[] routers = null!;
    private bool flip;

    [GlobalSetup]
    public async Task Setup()
    {
        var names = Enumerable.Range(0, Regions).Select(i => "r" + i).ToArray();
        provider = Bench.Build(factories: true, names);
        routers = names.Select(provider.GetRouter).ToArray();
        foreach (var router in routers)
        {
            Bench.Ok(await router.GoToAsync("/a"));
            Bench.Ok(await router.GoToAsync("/b"));
        }

        // The point of a region is its own history: check it before measuring anything.
        if (Regions > 1 && ReferenceEquals(routers[0].History, routers[1].History))
            throw new InvalidOperationException("Two regions share one history.");
    }

    [GlobalCleanup]
    public void Cleanup() => provider.Dispose();

    private string Next() { flip = !flip; return flip ? "/a" : "/b"; }

    /// <summary>One region after the other.</summary>
    [Benchmark(Baseline = true)]
    public async Task Sequential()
    {
        var url = Next();
        foreach (var router in routers)
            Bench.Ok(await router.GoToAsync(url));
    }

    /// <summary>All regions at once: each has its own gate, so they must not wait for one another.</summary>
    [Benchmark]
    public async Task Parallel_Regions()
    {
        var url = Next();
        var results = await Task.WhenAll(routers.Select(r => r.GoToAsync(url)));
        foreach (var result in results)
            Bench.Ok(result);
    }

    /// <summary>Looking up a region's router in the container.</summary>
    [Benchmark]
    public IRouter Resolve_Region() => provider.GetRouter("r" + (Regions - 1));

    /// <summary>A new container with its regions, resolved and used once.</summary>
    [Benchmark]
    public async Task ColdRegions()
    {
        var names = Enumerable.Range(0, Regions).Select(i => "c" + i).ToArray();
        using var cold = Bench.Build(factories: true, names);
        foreach (var name in names)
            Bench.Ok(await cold.GetRouter(name).GoToAsync("/a"));
    }
}

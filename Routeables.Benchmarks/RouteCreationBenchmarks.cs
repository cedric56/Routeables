using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;

namespace Routeables.Benchmarks;

/// <summary>
/// What it costs to declare a route and to create its page the first time and afterwards.
/// <c>Factories</c> = true is the path of a generated route (constructor factories); false is a hand-made
/// <c>RegisterRoute&lt;TView, TViewModel&gt;()</c>, which falls back to cached reflection.
/// </summary>
[MemoryDiagnoser]
public class RouteCreationBenchmarks
{
    [Params(false, true)]
    public bool Factories { get; set; }

    private ServiceProvider provider = null!;
    private IRouter router = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        provider = Bench.Build(Factories);
        router = provider.GetRequiredService<IRouter>();
        // Warm-up: the first routing of each route pays for the one-time caches; the steady state is measured below.
        foreach (var url in new[] { "/a", "/b", "/dep", "/orders/1", "/a" })
            Bench.Ok(await router.GoToAsync(url));
    }

    [GlobalCleanup]
    public void Cleanup() => provider.Dispose();

    /// <summary>Declaring the six routes of the fixtures in a service collection.</summary>
    [Benchmark]
    public int RegisterRoutes()
    {
        var services = new ServiceCollection();
        Bench.Register(services, Factories);
        return services.Count;
    }

    /// <summary>A new container, then the first routing: the cold start of an application.</summary>
    [Benchmark]
    public async Task ColdProviderAndFirstRouting()
    {
        using var cold = Bench.Build(Factories);
        Bench.Ok(await cold.GetRequiredService<IRouter>().GoToAsync("/a"));
    }

    // Each operation below goes to the partner route "/a" and then to the route measured: OperationsPerInvoke = 2
    // reports the cost of one routing. "/a" is the same in every row, so the rows compare the routes.

    /// <summary>Page and view model without constructor arguments.</summary>
    [Benchmark(OperationsPerInvoke = 2)]
    public async Task GoTo_Plain()
    {
        Bench.Ok(await router.GoToAsync("/a"));
        Bench.Ok(await router.GoToAsync("/b"));
    }

    /// <summary>View model with a service in its constructor.</summary>
    [Benchmark(OperationsPerInvoke = 2)]
    public async Task GoTo_WithConstructorDependency()
    {
        Bench.Ok(await router.GoToAsync("/a"));
        Bench.Ok(await router.GoToAsync("/dep"));
    }

    /// <summary>View model built from an address parameter ({id:int}); always the reflection path.</summary>
    [Benchmark(OperationsPerInvoke = 2)]
    public async Task GoTo_WithRouteParameter()
    {
        Bench.Ok(await router.GoToAsync("/a"));
        Bench.Ok(await router.GoToAsync("/orders/42"));
    }
}

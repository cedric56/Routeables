# Benchmarks

`Routeables.Benchmarks` measures the router with [BenchmarkDotNet](https://benchmarkdotnet.org). It is a console project outside the test suite: it
references `Routeables.Abstraction` and `Routeables.Routing` only, so it measures the shared route change core, not a UI toolkit (see
[host scenarios](host-scenarios.md) for what has to be checked on each host).

```
dotnet run -c Release --project Routeables.Benchmarks -- --filter "*"                  # everything (about 15 minutes)
dotnet run -c Release --project Routeables.Benchmarks -- --filter "*Navigation*" --job short
dotnet run -c Release --project Routeables.Benchmarks -- --leak                        # memory and disposal checks, exit code 0 = pass
```

Always run in `Release`, on a machine that is doing nothing else. Compare numbers only between runs on the same machine.

## What is measured

| Class | Question it answers |
|---|---|
| `RouteCreationBenchmarks` | What does declaring a route cost, and the cold start (new container + first route change)? What does creating a page cost for a plain view model, one with a constructor dependency, one with an address parameter? Each is run with `Factories = true` (what the source generator emits for a `[Routeable]` route) and `false` (a hand-made `RegisterRoute<TView, TViewModel>()`, cached reflection). |
| `NavigationBenchmarks` | Cost of `GoToAsync`, of the typed `GoToRouteAsync(view)`, of a page with a guard and a callback, of `Back`/`Forward`, `Push`/`Pop`, `MoveTo`, and of 8 route changes requested at once on one router. Run with a short history and a full one (the history has no limit, so a push on a long history costs the same as on a short one). |
| `HistoryLifecycleBenchmarks` | Filling a history with 20 pages and emptying it: creation and disposal of 20 pages. |
| `RegionBenchmarks` | Routing in 1, 4 and 16 independent regions, one after the other and all at once; looking a region up; cold start of N regions. One invocation is `Regions` route changes. |

Every benchmark runs `MemoryDiagnoser`: read the `Allocated` column next to the time. A benchmark whose routing fails throws instead of
measuring the failure. Rows with `OperationsPerInvoke = 2` go to a partner route and then to the measured one, and report the cost of one
routing; the partner is the same in every row.

## How to read the results

- **Generated vs reflection** (`Factories`): the difference in `GoTo_*` is what removing reflection saves per page; the difference in
  `ColdProviderAndFirstNavigation` is what it saves at start-up. `GoTo_WithRouteParameter` has no factory form, so only its partner route (`/a`) changes: the gap is smaller than for the other rows.
- **Regions**: divide a `Regions = 16` time by 16 and compare it to `Regions = 1`. If it is about the same, regions do not slow each other down.
  `Parallel_Regions` should not be slower than `Sequential` (each region has its own gate).
- **Same router, 8 at once** (`Concurrent_SameRouter_8`): route changes on one router run one after the other, all succeed. The time is about 8 times a single route change.
- **Allocations**: `Back_Forward` and `MoveTo` create no page; what they allocate is the cost of the history and the lifecycle callbacks alone.

## Memory checks (`--leak`)

Not timings: pass or fail, over 5 000 route changes each.

1. The history has one entry per route change, and none once it is erased.
2. View models dropped from the history can be collected (weak references).
3. Disposable view models are all disposed (created = disposed once the history is erased).
4. The managed heap does not grow with the number of route changes.

## Results

`RouteCreationBenchmarks`, BenchmarkDotNet 0.15.8, .NET 10.0.13, Windows 11, Intel Core i7-4790 (2014, 4 cores). A recent machine is faster.
`GoTo_*` rows are the cost of **one route change**; the other rows are one operation.

| Method | Factories | Mean | Error | Allocated |
|---|---|---|---|---|
| RegisterRoutes | false | 4.38 µs | 0.016 µs | 7.49 KB |
| RegisterRoutes | true | 4.18 µs | 0.018 µs | 7.10 KB |
| ColdProviderAndFirstNavigation | false | 40.35 µs | 0.51 µs | 37.19 KB |
| ColdProviderAndFirstNavigation | true | 33.51 µs | 0.55 µs | 34.87 KB |
| GoTo_Plain | false | 5.03 µs | 0.012 µs | 8.83 KB |
| GoTo_Plain | true | 3.97 µs | 0.015 µs | 7.54 KB |
| GoTo_WithConstructorDependency | false | 5.37 µs | 0.024 µs | 9.15 KB |
| GoTo_WithConstructorDependency | true | 4.27 µs | 0.010 µs | 7.84 KB |
| GoTo_WithRouteParameter | false | 7.55 µs | 0.050 µs | 11.98 KB |
| GoTo_WithRouteParameter | true | 6.75 µs | 0.049 µs | 11.33 KB |

What they say:

- A route change costs 4 to 7.5 µs and 7.5 to 12 KB. A frame at 60 images per second lasts 16 600 µs: the router is never the bottleneck.
- Factories (what the generator emits) save about 1 µs and 1.3 KB per routing (about 20 % of the time, 15 % of the memory). It is a real
  but small gain; the main reason to generate is that there is no reflection left (trimming, native AOT) and that errors appear at compile time.
- An address parameter (`{id:int}`) adds 2.5 to 3 µs: the address is read and converted.
- A constructor dependency adds about 0.3 µs.
- `Error` is at most 0.05 µs on 0.5 to 8 µs: the measurements are steady.
- Declaring routes (about 4 µs for six) is negligible; the cold start (about 35 µs) is a new container plus a first route change.

The other classes (routing, history, regions) have no published results yet. Run the whole suite and add its tables here.

## Limits

- The benchmarks do not draw anything: the cost of creating and laying out a real view on MAUI, Avalonia or Blazor is not measured, and it is
  usually far larger than the router's. Measure it with the host's own tools (see the host scenarios).
- The cold start is not a first start of the process: the one-time reflection caches are already filled by the warm-up. A real first launch costs more.
- They use `Transient` pages and an in-process container. A real application with a larger container or `Scoped` pages will differ.
- CI does not build this project. Build it with `dotnet build -c Release Routeables.Benchmarks` when the router changes.

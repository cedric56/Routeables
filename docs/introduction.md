# Introduction

**View-model-first routing for .NET, with one routing layer for Blazor, .NET MAUI, Avalonia and Uno Platform.**

Your view models depend on a single `IRouter` interface instead of framework-specific routing APIs, so the same routing code is shared across UI stacks. Routes are declared with attributes and wired by Roslyn source generators, and routing calls to a view model can be generated as strongly typed methods that mirror its constructor.

## Features

- **View-model-first routing**: route by route string with typed segments such as `/orders/{id:int}`, or by a view model's declared route.
- **Source-generated registration**: put `[Routeable("/path")]` on the view model (`[RouteableView]` on the view only when its name does not follow the convention) and call `AddGeneratedRoutes()`, with no manual wiring.
- **Generated typed routing**: `[RouteableViewModel(typeof(Caller))]` generates `GoTo{Name}Async` on the caller only, with the destination's constructor parameters as arguments, checked at compile time.
- **Back/forward history**: back, forward, pop, move to an entry, or return to the root.
- **Routing guards**: an async `CanLeaveAsync` can veto leaving a page.
- **Lifecycle hooks**: `IRouteable` and `IInitializeAsync` (plain and typed), plus standard `IDisposable` / `IAsyncDisposable` cleanup when an entry leaves the history.
- **Regions**: independent routing stacks (for example `main` and `side`), each with its own history.
- **Shared services**: dialogs, content dialogs, task dialogs, notifications and theme switching behind common interfaces.
- **Four UI hosts**: Blazor (Razor), .NET MAUI, Avalonia and Uno Platform (preview).
- **Optional MVVM helpers**: a view-model base for CommunityToolkit.Mvvm, opt in through an MSBuild property (ReactiveUI is planned, not shipped yet).

## Projects

| Project | Purpose |
| --- | --- |
| [`Routeables.Abstraction`](https://github.com/cedric56/Routeables/blob/master/Routeables.Abstraction/README.md) | Shared contracts: `IRouter`, `IRouteable`, `RouteableArgs`, attributes, lock and error contracts |
| [`Routeables.Services`](https://github.com/cedric56/Routeables/blob/master/Routeables.Services/README.md) | Optional UI service contracts: dialogs, content and task dialogs, notifications, theme |
| [`Routeables.Locking`](https://github.com/cedric56/Routeables/blob/master/Routeables.Locking/README.md) | Optional: the broker and the locking of the main entries |
| [`Routeables.Regions`](https://github.com/cedric56/Routeables/blob/master/Routeables.Regions/README.md) | Optional: `RegionBinding`, `RegionFollower`, `RegionLayout` |
| [`Routeables.Segments`](https://github.com/cedric56/Routeables/blob/master/Routeables.Segments/README.md) | Optional: routing by segments (`Orders?id=5/Detail`) |
| [`Routeables.Routing`](https://github.com/cedric56/Routeables/blob/master/Routeables.Routing/README.md) | The router, history, routing locking and DI registration |
| [`Routeables.Core`](https://github.com/cedric56/Routeables/blob/master/Routeables.Core/README.md) | Routing-aware view-model bases and runtime/environment info |
| [`Routeables.Generated`](https://github.com/cedric56/Routeables/tree/master/Routeables.Generated) | Roslyn source generators: route registration and typed routing methods |
| [`Routeables.Razor`](https://github.com/cedric56/Routeables/blob/master/Routeables.Razor/README.md) | Blazor integration |
| [`Routeables.Maui`](https://github.com/cedric56/Routeables/blob/master/Routeables.Maui/README.md) | .NET MAUI integration |
| [`Routeables.Avalonia`](https://github.com/cedric56/Routeables/blob/master/Routeables.Avalonia/README.md) | Avalonia integration |
| [`Routeables.Uno`](https://github.com/cedric56/Routeables/blob/master/Routeables.Uno/README.md) | Uno Platform integration (preview) |
| [`Samples`](https://github.com/cedric56/Routeables/tree/master/Samples) | Sample apps (see [Samples](guides/hosts.md#samples)) |
| [`Reshapes`](https://github.com/cedric56/Routeables/blob/master/Reshapes/README.md) | Optional look-and-feel packages: MudBlazor, CommunityToolkit.Maui, FluentAvalonia |
| [`Routeables.Benchmarks`](https://github.com/cedric56/Routeables/tree/master/Routeables.Benchmarks) | BenchmarkDotNet benchmarks and memory checks of the router (see [docs/benchmarks.md](benchmarks.md)) |
| [`Routeables.Test`](https://github.com/cedric56/Routeables/tree/master/Routeables.Test) | Unit tests (xunit v3) |
| [`docs`](index.md) | DocFX documentation site |

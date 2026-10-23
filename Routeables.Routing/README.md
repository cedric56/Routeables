# Routeables.Routing

`Routeables.Routing` implements the route change contracts of [`Routeables.Abstraction`](../Routeables.Abstraction/README.md): the router, its
history, the serialization of route changes, the lifecycle calls, the parsing of routes and the registration helpers the hosts use.

## Requirements

Targets **.NET 10** (`net10.0`), version `1.0.0-preview.1`, nullable reference types on. It references `Microsoft.Extensions.DependencyInjection`
and `Routeables.Abstraction`.

```xml
<ItemGroup>
  <ProjectReference Include="..\Routeables.Routing\Routeables.Routing.csproj" />
</ItemGroup>
```

```bash
dotnet build Routeables.Routing/Routeables.Routing.csproj
```

The router class itself is internal: an application uses `IRouter`, resolved from the container.

## Registering a router

`RegisterRouting` registers the router with its history, gate and lifecycle. By default they are singletons: one route change stack for the
application.

```csharp
using Routeables.Routing;

services.RegisterRouting();
```

On Blazor Server give each user circuit its own history:

```csharp
services.RegisterRouting(ServiceLifetime.Scoped);
```

A `Transient` router is refused (`ArgumentOutOfRangeException`): every resolve would get a separate history. For several independent
stacks use regions.

## Regions

```csharp
services.AddRegion("main");
services.AddRegion("side");
```

Each region is a keyed `IRouter` with its own history, gate and lifecycle (`Scoped` by default; pass a lifetime to change it). Resolve one
with `provider.GetRouter("side")` (the default router for `null`), or the keyed-service API.
`AddRegionLayout<TLayout>("content", "header", "menu")` (in the optional [`Routeables.Regions`](../Routeables.Regions/README.md)) registers the regions and a `RegionLayout`. A route names its region with
`[Routeable("/inbox", region: "side")]`. A host shows a region with its `RouteableRegion` control; see the
[main README](../README.md#regions) for the controls, `RegionFollower` and `RegionLayout`.

## Registering routes

With the source generator, `[RouteableView]` and `[Routeable]` produce the registrations and `AddGeneratedRoutes()` applies them. By hand:

```csharp
services.RegisterRouting();
services.RegisterRoute<HomeView, HomeViewModel>(route: "/");
services.RegisterRoute<OrderView, OrderViewModel>(route: "/orders/{id:int}");
```

A view and a view model are `Transient` by default; both take a lifetime (see [Lifetimes](../README.md#lifetimes)). `ROUT201` warns when a
route with parameters is registered `Scoped` or `Singleton`. A host's setup call (`UseRouteables`, `RouteableBuilder.CreateRouteableApp`)
registers the router already: do not register a second one.

## Routing behavior

- Resolve `IRouter` in a view model and call `GoToAsync(url)`, `GoToRouteAsync`, `GoBackAsync`, `ForwardAsync`, `PopAsync`,
  `GoBackToRootAsync`, `MoveToAsync` or `EraseHistoryAsync`. `GoToForResultAsync` waits for a page that gives a value back.
- Calls on one router run one after the other; several route changes requested at once are all executed, one after the other.
- Guards and lifecycle callbacks run before the history changes. A refused or failed routing commits nothing and the previous page is
  restored. The result is a `RouteableResult` (`Success`, `Cancelled`, `Exception`).
- The history keeps `RouterHistory.MaxSize` (20) entries. Entries that leave it are disposed. `GoBackAsync`, `ForwardAsync` and `MoveToAsync` only
  move the cursor.
- `Routing` and `Routed` are raised for each route change. A handler runs while the router holds its gate: it must not start a route change. A
  handler that throws never fails the route change; errors go to the `IRouteableErrorSink` you register, or to `System.Diagnostics.Trace`.
- `RouteParser`, `RouteableArgs`, `RouteableEntry` / `RouteableEntry<T>` (a menu entry) and `RouterTypedExtensions` (`GoToRouteAsync<TViewModel, TData>`)
  are public here.

## Performance

The router's cost is measured with BenchmarkDotNet (`Routeables.Benchmarks`): about 4 to 7.5 µs and 7.5 to 12 KB per routing on an
i7-4790. See [docs/benchmarks.md](../docs/benchmarks.md).

## Related projects

- [`Routeables.Abstraction`](../Routeables.Abstraction/README.md): contracts, arguments, lifecycle interfaces.
- [`Routeables.Core`](../Routeables.Core/README.md): view-model bases.
- Hosts: [Avalonia](../Routeables.Avalonia/README.md), [MAUI](../Routeables.Maui/README.md), [Razor](../Routeables.Razor/README.md).

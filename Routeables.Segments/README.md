# Routeables.Segments

Optional routing by segments, in the manner of Prism: `Orders?status=open/Detail?id=5` runs the segments one after the other, each on its router and with only its own query parameters. A project that only uses `IRouter` does not carry it.

Namespace `Routeables.Segments`. Targets **.NET 10** (`net10.0`), version `1.0.0-preview.1`. It references `Routeables.Abstraction` and `Routeables.Routing`.

```csharp
services.RegisterRouting();
services.AddRouteCoordinator();                      // routing alone does not register the coordinator
services.RegisterSegment<OrdersViewModel>("Orders"); // the view model is registered with RegisterRoute

await coordinator.CoordinateAsync("Orders?status=open/Detail?id=5");
```

| Syntax | Meaning |
| --- | --- |
| `Name?k=v&k=w` | Pushes the segment registered under `Name` |
| `region:Name` | Hands the segment to the router of that region |
| `..` | Goes back one entry (`../Edit?id=5`: back, then push) |
| leading `/` | Absolute: the history of every router involved is cleared first |

| Type | Purpose |
| --- | --- |
| `IRouteCoordinator` | Runs a coordination; raises `Coordinating` and `Coordinated` |
| `RouteCoordination` | A parsed or fluent (`RouteCoordination.Create()`) list of segments |
| `RouteSegment`, `RouteSegmentKind` | One segment of a coordination |
| `AddRouteCoordinator(lifetime)` | Registers the coordinator; use the lifetime of the routers it drives (`Scoped` on Blazor Server) |
| `RegisterSegment<TViewModel>(name)` | Makes a view model reachable under a segment name |
